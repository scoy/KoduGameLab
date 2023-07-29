// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.

#define NEW_GET_WORLDS

using System;
using System.IO;
using System.Collections.Generic;
using System.Diagnostics;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using Boku.Base;
using Boku.Web;
using Boku.Common;
using Boku.Common.Sharing;
using Boku.Common.Xml;

using BokuShared;

namespace Boku.Common
{
    //helper class for containing info regarding a download for an item that may not be loaded in the current 
    //browser - used when linked level dependencies are donwloaded and we can't guarantee the item is on the 
    //current page
    class OffPageLevelInfo
    {
        public Guid WorldId;
        public LevelDownloadOffPageCompleteEvent downloadCallback;
    }

    /// <summary>
    /// An ILevelBrowser for browsing levels on the community server
    /// </summary>
    public class CommunityLevelBrowser : ILevelBrowser
    {
        #region Private

        object synch = new object();
        public object Synch { get { return synch; } }

        List<ILevelSetQuery> queries = new List<ILevelSetQuery>();
        List<LevelMetadata> allLevels = new List<LevelMetadata>();
        List<LevelMetadata> queuedThumbnailLoads = new List<LevelMetadata>();
        int thumbnailLoadOpCount;

        class LevelBrowserState
        {
            public LevelMetadata level;
            public LevelDownloadCompleteEvent downloadCallback = null;
            public ThumbnailDownloadCompleteEvent thumbnailCallback;
        }

        #endregion Private

        #region Public

        public bool Working
        {
            get { return pagingOpCount > 0; }
        }


        /// <summary>
        /// Start deleting a level from the community server.  You must have adequate permissions or the server will deny your request.
        /// </summary>
        /// <param name="worldId"></param>
        /// <param name="callback"></param>
        /// <param name="param"></param>
        /// <returns></returns>
        public bool StartDeletingLevel(
            LevelMetadata level,
            Genres bucket,
            BokuAsyncCallback callback,
            object param)
        {
            // Remove the level from the browser's current set of worlds.
            // Note that this assumes that the delete is just going to 
            // work.  We do this so that the UI feels very responsive.
            int index = IndexOf(level.WorldId);
            if (index >= 0)
            {
                LevelMetadata level2 = allLevels[index];
                allLevels.RemoveAt(index);
                LevelRemoved(level2);
            }

            //CommunityServices.DeleteWorld(level);

            //Build delete world args
            var args = new
            {
                worldId = level.WorldId.ToString(),
                creator = Auth.CreatorName,
                pin = Auth.Pin,
                saveTime = level.SaveTime
            };
            KoduService.DeleteWorld(args,(returnedObject)=> { 
                if(returnedObject==null)
                {
                    //delete failed
                }
                //4scoy. Handle success/failure
                // For now, don't do anything.  On success, there's nothing to do.
                // As noted on the top of the function, we remove the level from the browser
                // before even trying to delete it. What should happen here is that we should
                // re-add the level to the browser and give the user a error message telling
                // them that the delete failed.  Right now, the only way this should fail is
                // if the services go offline between the Search call and the user choosing to
                // delete a world.  This should be very rare.  Additionally, the only result
                // of this is that the next time the user comes into the Community, they will
                // notice that the world they tried to delete is still there.  Should do this
                // right for V2 since we handle dialogs much more sanely there.
            });

            // In this case, the callback is just prompting the browser to start fetching
            // levels.  No arg is needed.
            callback(null);

            return true;    // Looks like this is ignored?

            /*
            return 0 != Web.Community.Async_DelWorldData2(
                worldId,
                Auth.Pin,
                callback,
                param);
            */ 
        }   // end of StartDeletingLevel()
            
        public void Update()
        {
            lock (Synch)
            {
                foreach (LevelSetQuery query in queries)
                {
                    query.Update();
                }
            }

            if (thumbnailLoadOpCount == 0 && queuedThumbnailLoads.Count > 0)
            {
                // Pull from the end of the list to service newer requests first.
                LevelMetadata level = queuedThumbnailLoads[queuedThumbnailLoads.Count - 1];
                queuedThumbnailLoads.RemoveAt(queuedThumbnailLoads.Count - 1);

                thumbnailLoadOpCount += 1;
                KoduService.GetThumbnail(level.WorldId,level.ThumbnailUrl,
                    (responseStream) =>
                    {
                        if (responseStream == null)
                        {
                        // Failed.  Nothing to do here.
                        }
                        else
                        {
                            using (MemoryStream ms = new MemoryStream())
                            {
                                responseStream.CopyTo(ms);
                                ms.Seek(0, SeekOrigin.Begin);
                                level.Thumbnail.Texture = Storage4.TextureLoad(ms);
                                level.Thumbnail.Loading = false;
                            }

                    }

                    // This will trigger a UI refresh which will either use the 
                    // new thumbnail we just created or, if we had a failure, this
                    // will just use the MissingImage graphic.
                    LevelBrowserState state = (LevelBrowserState)level.BrowserState;
                        if (state.thumbnailCallback != null)
                            state.thumbnailCallback(level);
                        state.thumbnailCallback = null;

                        thumbnailLoadOpCount -= 1;
                    });//end of callback and func

            }   // end if we have any thumbnails to load.

            // If the browser list of worlds gets long, we end up using a lot of graphics memory storing 
            // the thumbnail textures.  So, look at the list and free up thumbnails on the worlds that
            // are farthest away from the cursor window.
            // maxLevelsInMemoryCushion is the number of worlds we keep textures for on each side of the 
            // cursor's current position.  If a user is just scrolling forward, this will limit us to
            // about 100 textures in memory.  If the user is scrolling forward and backward we may have 

            int maxLevelsInMemoryCushion = 100;
            if (queries.Count > 0)
            {
                LevelSetQuery query = queries[0] as LevelSetQuery;
                if (query != null)
                {
                    int curPosition = query.CurrentCursorPosition;
                    // Are we deep enough in the list to get rid of earlier textures?
                    if (curPosition > maxLevelsInMemoryCushion)
                    {
                        // Free up and thumbnails from earlier levels.
                        for (int i = 0; i < curPosition - maxLevelsInMemoryCushion; i++)
                        {
                            FreeThumbnail(i);
                        }
                    }
                    // Have we scrolled deep into the list and then come back to the beginning?
                    if (curPosition + maxLevelsInMemoryCushion < allLevels.Count)
                    {
                        for (int i = curPosition + maxLevelsInMemoryCushion; i < allLevels.Count; i++)
                        {
                            FreeThumbnail(i);
                        }
                    }
                }
            }
        }   // end of Update()

        void FreeThumbnail(int i)
        {
            // Ensure we actually have a list this long.
            if (allLevels.Count > i)
            {
                LevelMetadata level = allLevels[i];
                if (level != null)
                {
                    // Note that we're call Dispose on the Thumbnail, not its Texture.  The 
                    // Reason is that the Texture getter returns the MissingImage texture if
                    // the real texture isn't loaded.  This Dispose call does nothing if the
                    // real texture isn't there.
                    level.Thumbnail.Dispose();
                }
            }
        }   // end of FreeThumbnail()

        public void Shutdown()
        {
            Web.Community.Async_AbortAll();
        }

        public void Reset()
        {
            lock (Synch)
            {
                pagingFirst = 0;
                pagingEndReached = false;

                allLevels.Clear();

                foreach (ILevelSetQuery query in queries)
                {
                    query.Clear();
                }
            }
        }

        public ILevelSetCursor OpenCursor(
            Guid desiredSelection,
            ILevelSetSorter sorter,
            ILevelSetFilter filter,
            LevelSetQueryEvent notifyFetchingCallback,
            LevelSetQueryEvent notifyFetchCompleteCallback,
            LevelSetCursorShifted shiftCallback,
            LevelSetCursorJumped jumpCallback,
            LevelSetCursorAddition additionCallback,
            LevelSetCursorRemoval removalCallback,
            int size)
        {
            LevelSetCursor cursor;

            lock (Synch)
            {
                ILevelSetQuery query = new LevelSetQuery(
                    sorter,
                    filter, 
                    this, 
                    notifyFetchingCallback, 
                    notifyFetchCompleteCallback);

                queries.Add(query);

                cursor = new LevelSetCursor(
                    this,
                    desiredSelection,
                    query,
                    shiftCallback,
                    jumpCallback,
                    additionCallback,
                    removalCallback,
                    size);
                
                query.AddCursor(cursor);
            }

            return cursor;
        }

        public void CloseCursor(ref ILevelSetCursor icursor)
        {
            LevelSetCursor cursor = icursor as LevelSetCursor;

            if (cursor != null)
            {
                lock (Synch)
                {
                    int index = queries.IndexOf(cursor.Query);

                    if (index >= 0)
                    {
                        ILevelSetQuery query = queries[index];
                        if (0 == query.RemoveCursor(cursor))
                        {
                            queries.RemoveAt(index);
                        }
                    }
                }

                icursor = null;
            }
        }

        public void LoadQuery(ILevelSetQuery query)
        {
            lock (Synch)
            {
                foreach (LevelMetadata level in allLevels)
                {
                    query.LevelAdded(level);
                }
            }
        }

        #region Paging State Variables

        int pagingOpCount;
        int pagingFirst = 0;
        const int kPagingPageSize = 30;
        bool pagingEndReached;

        #endregion

        public bool StartFetchingMore(ILevelSetQuery query)
        {
            // Prevent new ops from being starting while one is already pending.
            if (pagingOpCount == 0 && !pagingEndReached)
            {

                LevelSetSorterBasic basicSorter = query.Sorter as LevelSetSorterBasic;
                LevelSetFilterByKeywords filter = query.Filter as LevelSetFilterByKeywords;

#if NEW_GET_WORLDS
                string sortBy = basicSorter.SortBy.ToString().ToLower();
                if (sortBy == "rank")
                {
                    sortBy = "downloads";
                }
                string sortDir = basicSorter.SortDirection == SortDirection.Ascending ? "asc" : "desc";
                string keywords = filter.SearchString;
                string creator = (filter.FilterGenres & Genres.MyWorlds) != 0 ? Auth.CreatorName : null;

                // In the Community menu we can't rely on the MyWorlds bit to indicate that MyWorlds
                // has been chosen since it's included in All.  For the Community
                creator = filter.FilterGenres == Genres.All ? null : Auth.CreatorName;
                creator = Auth.CreatorName;

                // In the Community menu we can't rely on the MyWorlds bit to indicate that MyWorlds
                // has been chosen since it's included in All.  We also can't use the grid settings
                // because they don't seem to be right.  I suspect that it's due to the selection
                // state not filtering through on the first frame.
                //
                // For the Community browser we start by assuming MyWorlds is chosen and clear it if
                //      - Filter is set to All
                //      - MyWorlds bit is clear
                //      - CreatorName is Guest
                //
                // creatorName is then passed to the server where, if it's not null, we use the "MyWorlds" version of Search and Get. 
                string creatorName = Auth.CreatorName;
                if (filter.FilterGenres == Genres.All || ((filter.FilterGenres & Genres.MyWorlds) == 0) || Auth.CreatorName == Auth.DefaultCreatorName)
                {
                    creatorName = null;
                }
                Debug.WriteLine(creatorName);

                pagingOpCount += 1;


                // Build search arguments.
                var args = new
                {
                    first = pagingFirst,
                    count = kPagingPageSize,
                    sortBy = sortBy,
                    sortDir = sortDir,
                    range = "all",
                    keywords = keywords,
                    creator = creator,
                    creatorName = creatorName
                };
                KoduService.Search(args, (object results) => {
                    //4scoy NOTE in case of fail (results==null) we still pass
                    //to FetchComplete(). It handles that case.
                    //This should probably be done differently.
                    FetchComplete((string)results);

                    // If we got nothing from the start, then the community is not available.
                    // Exit back out to main menu.
                    if (pagingFirst == 0 && results == null)
                    {
                        // Switch back to the MainMenu.
                        BokuGame.bokuGame.community.Deactivate();
                        BokuGame.bokuGame.mainMenu.Activate();
                    }

                });
                return true;
#else

                // This is a bit of a hack/limitation. For the moment, the community server only
                // supports filtering by genre and sorting on the basic fields. For this limitation
                // to be removed, we must support all sorters and filters on the server side, and
                // send them up with every query.
                if (String.IsNullOrEmpty(filter.SearchString)
                    || filter.SearchString.Split(new[] { ' ' }, System.StringSplitOptions.RemoveEmptyEntries).Length < 1)
                {
                    //if no keywords do old style display
                    if (0 != Web.Community.Async_GetPageOfLevels(
                        filter != null ? filter.FilterGenres : Genres.All,
                        basicSorter != null ? basicSorter.SortBy : SortBy.Date,
                        basicSorter != null ? basicSorter.SortDirection : SortDirection.Descending,
                        pagingFirst,
                        kPagingPageSize + 1,    // Make this match new code.
                        FetchComplete,
                        query))
                    {
                        pagingOpCount += 1;
                        return true;
                    }
                }
                else
                {
                    //use keyword search version
                    if (0 != Web.Community.Async_GetSearchPageOfLevels(
                        filter != null ? filter.FilterGenres : Genres.All,
                        filter.SearchString,
                        basicSorter != null ? basicSorter.SortBy : SortBy.Date,
                        basicSorter != null ? basicSorter.SortDirection : SortDirection.Descending,
                        pagingFirst,
                        kPagingPageSize,
                        FetchComplete,
                        query))
                    {
                        pagingOpCount += 1;
                        return true;
                    }
                }
                
#endif
            }
            return false;
        }   // end of StartFetchingMore()

        public void StartDownloadingThumbnail(LevelMetadata level, ThumbnailDownloadCompleteEvent callback, bool lowPriority)
        {
            if (level != null && !level.Thumbnail.IsLoaded && !level.Thumbnail.Loading)
            {
                LevelBrowserState state = (LevelBrowserState)level.BrowserState;
                state.thumbnailCallback = callback;

                // If it already exists in the queue, move it to the end.
                queuedThumbnailLoads.Remove(level);
                queuedThumbnailLoads.Add(level);
            }

            // Only keep a max of 20 pending thumbnail loads, that way of we're just scrolling
            // to the end of the list, we can discard many of these requests as they pass out of view.
            while (queuedThumbnailLoads.Count > 20)
            {
                LevelBrowserState state = (LevelBrowserState)queuedThumbnailLoads[0].BrowserState;
                state.thumbnailCallback = null;
                queuedThumbnailLoads.RemoveAt(0);
            }
        }

        public bool StartDownloadingWorld(LevelMetadata level, LevelDownloadCompleteEvent callback)
        {
            var args = new
            {
                worldId = level.WorldId.ToString()
            };
            level.DownloadState = LevelMetadata.DownloadStates.InProgress;
            KoduService.DownloadWorld(args, (responseStream) =>{

                if(responseStream==null)
                {
                    // Failed.
                    // Show the failed icon to the user in the UI.
                    level.DownloadState = LevelMetadata.DownloadStates.Failed;

                    return;
                }

                // Create a path to the imports folder.  Note the name of the file really
                // doesn't matter.
                string path = Path.Combine(Storage4.UserLocation, "Imports", "Temp.Kodu2");

                // If an error left a file there, delete it.
                Storage4.Delete(path);

                // Write the file.
                using (FileStream fs = new FileStream(path, FileMode.Create))
                {
                    responseStream.CopyTo(fs);
                    fs.Close();
                }

                // Trigger Kodu's import system.
                List<Guid> importedLevels = new List<Guid>();
                bool importOk = LevelPackage.ImportAllLevels(importedLevels);

                // Set the icon on the world tile to match the result of the import.
                if (importOk)
                {
                    level.DownloadState = LevelMetadata.DownloadStates.Complete;
                }
                else
                {
                    level.DownloadState = LevelMetadata.DownloadStates.Failed;
                }

            });

            //CommunityServices.DownloadWorld(level);
            // 4scoy What is this doing?  Do we need this?
            // As the code sits right now, the callback is never called.
            // Looking at the code, it appears to be related to downloading linked levels.
            // In the old code, when a level was downloaded we also followed any links 
            // and downloaded all the levels linked to it.  In the new system, all the
            // linked levels should be part of the .Kodu2 file and will get downloaded
            // and imported as a unit so chasing the links is no longer needed.
            //LevelBrowserState state = (LevelBrowserState)level.BrowserState;
            //state.downloadCallback = callback;

            /*
            LevelBrowserState state = (LevelBrowserState)level.BrowserState;
            state.downloadCallback = callback;
            level.DownloadState = LevelMetadata.DownloadStates.InProgress;
            return 0 != Web.Community.Async_GetWorldData(level.WorldId, GetWorldDataCallback, level);
            */

            return true;
        }   // end of StartDownloadingWorld()

        //similiar to StartDownloadingWorld, but operates assuming we can't rely on the current browser page to contain the level
        //all world references will be through Guids instead of LevelMetadata until the download completes
        public bool StartDownloadingOffPageWorld(Guid worldId, LevelDownloadOffPageCompleteEvent callback)
        {            
            OffPageLevelInfo downloadInfo = new OffPageLevelInfo();
            downloadInfo.WorldId = worldId;
            downloadInfo.downloadCallback = callback;

            return 0 != Web.Community.Async_GetWorldData(worldId, GetOffPageWorldDataCallback, downloadInfo);
        }

        public void MetadataUpdated(LevelMetadata level)
        {
        }

        private void GetOffPageWorldDataCallback(AsyncResult result)
        {
            OffPageLevelInfo downloadInfo = (OffPageLevelInfo)result.Param;

            if (result.Success)
            {
                AsyncResult_GetWorldData data = result as AsyncResult_GetWorldData;
                if (data != null)
                {
                    data.World.Data.WorldId = data.World.Info.WorldId;
                    downloadInfo.downloadCallback(data.World.Data, data.World.Info.ThumbnailBytes, downloadInfo.WorldId);
                }
                else
                {
                    downloadInfo.downloadCallback(null, null, downloadInfo.WorldId);
                }
            }
            else
            {
                downloadInfo.downloadCallback(null, null, downloadInfo.WorldId);
            }
        }

        private void GetWorldDataCallback(AsyncResult result)
        {
            LevelMetadata level = (LevelMetadata)result.Param;
            LevelBrowserState state = (LevelBrowserState)level.BrowserState;

            if (result.Success)
            {
                AsyncResult_GetWorldData data = result as AsyncResult_GetWorldData;
                if (data != null)
                {
                    data.World.Data.WorldId = data.World.Info.WorldId;
                    level.DownloadState = LevelMetadata.DownloadStates.Complete;
                }
                else
                {
                    level.DownloadState = LevelMetadata.DownloadStates.Failed;
                }

                state.downloadCallback(data.World.Data, data.World.Info.ThumbnailBytes, level);
            }
            else
            {
                level.DownloadState = LevelMetadata.DownloadStates.Failed;
                state.downloadCallback(null, null, level);
            }
        }


        private int IndexOf(Guid worldId)
        {
            int index = -1;
            foreach (LevelMetadata level in allLevels)
            {
                index += 1;
                if (level.WorldId == worldId)
                    return index;
            }
            return -1;
        }

#if NEW_GET_WORLDS

        /// <summary>
        /// Callback for fetching for community browser.
        /// This is the new services version so we need to recreate the LevelMetadata structures
        /// from the passed in result string.
        /// </summary>
        /// <param name="results"></param>
        public void FetchComplete(string results)
        {
            // If no results, just bail.
            if (results == null)
            {
                pagingEndReached = true;
                return;
            }
            else
            {
                JsonSerializerSettings settings = new JsonSerializerSettings();
                settings.DateParseHandling = DateParseHandling.None;
                Newtonsoft.Json.Linq.JContainer array = JsonConvert.DeserializeObject(results, settings) as Newtonsoft.Json.Linq.JContainer;

                int count = 0;
                foreach (JToken token in array)
                {
                    LevelMetadata level = new LevelMetadata();

                    level.WorldId = new Guid(token.Value<string>("WorldId"));
                    level.Name = token.Value<string>("Name");
                    level.Description = token.Value<string>("Description");
                    level.Checksum = token.Value<string>("Checksum");
                    level.Creator = token.Value<string>("Creator");
                    level.Downloads = token.Value<int>("Downloads");
                    // The Community sorts on Modified which is slightly different than LastWriteTime.
                    // So we use LastWriteTime as the equivalent of Modified.  It would be nice if we
                    // changed LastWriteTime to Modified but that would (maybe) break back compat by
                    // changing LevelMetadata.  On the other hand, since LastWriteTime is only ever
                    // filled in when browsing the Community, maybe it's worth a try.
                    // TODO (scoy) Change LastWriteTime to Modified in LevelMetadata and see what breaks.
                    level.LastWriteTime = token.Value<DateTime>("Modified");
                    level.LastSaveTime = token.Value<DateTime>("LastSaveTime");
                    level.SaveTime = token.Value<string>("SaveTime");

                    level.ThumbnailUrl = token.Value<string>("ThumbnailUrl");
                    level.DataUrl = token.Value<string>("DataUrl");//not used. May be removed service side.

                    if (IndexOf(level.WorldId) == -1)
                    {
                        LevelBrowserState state = new LevelBrowserState();
                        state.level = level;
                        level.BrowserState = state;

                        level.Browser = this;
                        allLevels.Add(level);
                        LevelAdded(level);
                        count += 1;
                    }
                }

                // If we didn't get a full page, must be at end.  We used to test
                // against total number of levels but that turns out to be a bit slow.
                if (count < kPagingPageSize)
                {
                    pagingEndReached = true;
                }

                pagingFirst += count;
            }

            pagingOpCount -= 1;
            // Turns off "Fetching" message.
            BokuGame.bokuGame.community.CursorFetchCompleteCallback(null);

        }   // end of FetchComplete()

#else

        private void FetchComplete(AsyncResult ar)
        {
            AsyncResult_GetPageOfLevels result = (AsyncResult_GetPageOfLevels)ar;

            if (result.Success)
            {
                int count = 0;
                foreach (LevelMetadata level in result.Page.Listing)
                {
                    if (IndexOf(level.WorldId) == -1)
                    {
                        LevelBrowserState state = new LevelBrowserState();
                        state.level = level;
                        level.BrowserState = state;

                        level.Browser = this;
                        allLevels.Add(level);
                        LevelAdded(level);
                        count += 1;
                    }
                }

                if (result.Page.First >= result.Page.Total)
                    pagingEndReached = true;

                pagingFirst += count;
            }
            else
            {
                // An error occured, stop trying.
                pagingEndReached = true;
            }

            // Turns off "Fetching" message.
            BokuGame.bokuGame.community.CursorFetchCompleteCallback(null);

            pagingOpCount -= 1;
        }   // end of FetchComplete()
        
#endif

        private void GotThumbnail(AsyncResult ar)
        {
            AsyncResult_Thumbnail result = (AsyncResult_Thumbnail)ar;
            LevelMetadata level = (LevelMetadata)result.Param;
            LevelBrowserState state = (LevelBrowserState)level.BrowserState;

            if (result.Success)
            {
                MemoryStream stream = new MemoryStream(result.ThumbnailBytes);
                level.Thumbnail.Texture = Storage4.TextureLoad(stream);
                level.Thumbnail.Loading = false;
            }
            else
            {
                // TODO: Set thumbnail to use missing icon.
            }

            if (state.thumbnailCallback != null)
                state.thumbnailCallback(level);
            state.thumbnailCallback = null;

            thumbnailLoadOpCount -= 1;
        }

        public void GotThumbnail(IAsyncResult ar, LevelMetadata level)
        {
            LevelBrowserState state = (LevelBrowserState)level.BrowserState;

            if (state.thumbnailCallback != null)
                state.thumbnailCallback(level);
            state.thumbnailCallback = null;

            thumbnailLoadOpCount -= 1;
        }   // end of GotThumbnail()

        private void LevelAdded(LevelMetadata level)
        {
            if (IsAlreadyDownloaded(level))
            {
                level.DownloadState = LevelMetadata.DownloadStates.Complete;
                level.Genres |= Genres.Downloads;
            }

            foreach (LevelSetQuery query in queries)
            {
                query.LevelAdded(level);
            }
        }

        private void LevelRemoved(LevelMetadata level)
        {
            foreach (LevelSetQuery query in queries)
            {
                query.LevelRemoved(level);
            }
        }

        private bool IsAlreadyDownloaded(LevelMetadata level)
        {
            bool isAlreadyDownloaded = false;

            string filename = BokuGame.Settings.MediaPath + BokuGame.DownloadsPath + level.WorldId.ToString() + @".Xml";
            
            if (Storage4.FileExists(filename, StorageSource.UserSpace))
            {
                XmlWorldData xml = XmlWorldData.Load(filename, XnaStorageHelper.Instance);
                if (xml != null)
                {
                    LevelMetadata local = LevelMetadata.CreateFromXml(xml);

                    DateTime levelSaveTime = DateTime.Parse(level.SaveTime);

                    isAlreadyDownloaded = 
                        local.WorldId == level.WorldId &&
                        local.Creator == level.Creator &&
                        local.LastWriteTime >= levelSaveTime;
                }
            }

            return isAlreadyDownloaded;
        }

        #endregion

        public void AddLevel(LevelMetadata level)
        {
            Debug.Fail("not supported");
        }

        public void RemoveLevel(LevelMetadata level)
        {
            Debug.Fail("not supported");
        }
    }
}

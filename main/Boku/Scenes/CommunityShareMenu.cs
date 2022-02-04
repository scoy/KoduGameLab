// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.


using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using BokuShared.Wire;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

using Boku.Base;
using Boku.Common;
using Boku.Common.Xml;
using Boku.Common.Gesture;
using Boku.Common.Sharing;
using Boku.UI2D;
using Boku.Fx;
using Boku.Web;

using Point = Microsoft.Xna.Framework.Point;

namespace Boku
{
    /// <summary>
    /// Handles dialogs used for sharing levels.
    /// 
    /// Half of the functionality has been replaced.  So now, this class is striclty
    /// being used just to display error and warning dialogs.  All of the actual
    /// sharing is done elsewhere.
    /// 
    /// </summary>
    public class CommunityShareMenu : GameObject, INeedsDeviceReset
    {
        private LevelMetadata CurWorld;

        public CommunityShareMenu()
        {
        }   // end of c'tor

        /// <summary>
        /// Recreate render targets
        /// </summary>
        /// <param name="device"></param>
        public void DeviceReset(GraphicsDevice device)
        {
        }

        public void Update()
        {
            if (Active)
            {
            }

        }   // end of Update()

        public void DeactivateMenu(ModularMessageDialog dialog)
        {
            //close the dialog
            dialog.Deactivate();
            Deactivate();
        }

        private void ShowWarning(string text)
        {
            //handler for "continue" - user wants to play anyway
            ModularMessageDialog.ButtonHandler handlerA = delegate(ModularMessageDialog dialog)
            {
                //close the dialog
                dialog.Deactivate();
            };

            if (null == text)
            {
                text = "";
            }
            string labelA = Strings.Localize("textDialog.ok");
            ModularMessageDialogManager.Instance.AddDialog(text, handlerA, labelA);
        }

        //helper functions to display dialogs
        public void ShowNoCommunityDialog()
        {
            string text = Strings.Localize("miniHub.noCommunityMessage");
            string labelA = Strings.Localize("textDialog.back");
            ModularMessageDialogManager.Instance.AddDialog(text, DeactivateMenu, labelA);
        }
        public void ShowShareErrorDialog(string error)
        {
            string text =String.Format("{0} {1}",Strings.Localize("miniHub.noSharingMessage"),error);
            string labelA = Strings.Localize("textDialog.back");
            ModularMessageDialogManager.Instance.AddDialog(text, DeactivateMenu, labelA);
        }
        public void ShowBrokenLevelShareWarning()
        {
            ShowWarning(Strings.Localize("loadLevelMenu.brokenLevelShareMessage"));
        }
        public void ShowConfirmLinkedShareDialog()
        {
            // Handler for if user agrees to share all levels.
            ModularMessageDialog.ButtonHandler handlerA = delegate(ModularMessageDialog dialog)
            {
                //close the dialog
                dialog.Deactivate();

                ContinueCommunityShare();
            };

            string text = Strings.Localize("loadLevelMenu.confirmLinkedShareMessage");
            string labelA = Strings.Localize("textDialog.yes");
            string labelB = Strings.Localize("textDialog.no");
            ModularMessageDialogManager.Instance.AddDialog(text, handlerA, labelA, DeactivateMenu, labelB);
        }
        public void ShowShareSuccessDialog()
        {
            string text = Strings.Localize("miniHub.shareSuccessMessage");
            string labelB = Strings.Localize("textDialog.back");
            ModularMessageDialogManager.Instance.AddDialog(text, null, null, DeactivateMenu, labelB);
        }


        public void PopupOnCommunityShare(LevelMetadata level)
        {
            // Check if level has links.
            if (level.LinkedToLevel != null || level.LinkedFromLevel != null)
            {
                // Check if the chosen level has any broken links - if so, warn the player.
                LevelMetadata brokenLevel = null;
                bool forwardsLinkBroken = false;
                if (level.FindBrokenLink(ref brokenLevel, ref forwardsLinkBroken))
                {
                    ShowBrokenLevelShareWarning();
                }
                else
                {
                    // Prompt to confirm linked share.
                    ShowConfirmLinkedShareDialog();
                }
            }
            else
            {
                // Not a linked level, share as per normal.
                ContinueCommunityShare();
            }
        }   // end of PopupOnCommunityShare()

        internal void ContinueCommunityShare()
        {
            LevelMetadata level = CurWorld;

            // Always force us to save starting with the first level in the chain.
            level = level.FindFirstLink();

            //Prepare level for upload
            if (string.IsNullOrWhiteSpace(level.SaveTime))
            {
                level.SaveTime = level.LastSaveTime.ToString("yyyy'-'MM'-'dd'T'HH':'mm':'ss'.'fff'Z'");
                level.Checksum = BokuShared.Auth.CreateChecksumHash(BokuShared.Auth.CreatorName, BokuShared.Auth.Pin, level.SaveTime);
            }

            // Generate temp Kodu2 file.
            string pathToKodu2File = Path.Combine(Storage4.UserLocation, LevelPackage.ExportsPath, level.WorldId.ToString() + ".Kodu2");
            LoadLevelMenu.Shared.ExportLevel(level, pathToKodu2File);

            // Get paths for image files.
            string pathToThumb = Path.Combine(Storage4.UserLocation, @"Content\Xml\Levels\MyWorlds", level.WorldId.ToString() + ".Jpg");
            string pathToLarge = Path.Combine(Storage4.UserLocation, @"Content\Xml\Levels\MyWorlds", level.WorldId.ToString() + "_800.Jpg");


            // Give the community server the metadata about the level we want to upload.
            var args = new
            {
                worldId = level.WorldId.ToString(),
                //created = level.LastWriteTime.ToUniversalTime().ToString("yyyy'-'MM'-'dd'T'HH':'mm':'ss'.'fff'Z'"),//bogus
                name = level.Name,
                creator = level.Creator,
                saveTime = level.SaveTime,
                checksum = level.Checksum,
                numLevels = level.CalculateTotalLinkLength(),
                description = level.Description,
                pin = BokuShared.Auth.Pin,
                clientVersion = Program2.ThisVersion.ToString(),
            };

            KoduService.ShareRequestState = KoduService.RequestState.Pending;
            KoduService.UploadWorld(args, pathToKodu2File, pathToThumb, pathToLarge,(response) =>{
                if(response==null)
                {
                    //failed
                    KoduService.ShareRequestState = KoduService.RequestState.Error;
                    //todo handle reason?
                }
                else
                {
                    KoduService.ShareRequestState = KoduService.RequestState.Complete;
                }

                
                //4scoy
                //It looks to me like we should be calling 
                //Callback_PutWorldData here.
                // NOTE This is no longer needed. Previously, when we uploaded a level that was
                // part of a linked chain we would traverse the chain and upload every level in
                // the chain.  That is what this call was doing.  Instead, we now bundle all the
                // linked levels into the .Kodu2 file and upload them as a unit.
                // (scoy) TODO Once the new network code is stable, all this should be removed.

                // Clean up.
                // Delete the temp Kodu2 file.
                File.Delete(pathToKodu2File);

            });
            //CommunityServices.ShareWorld(level);

            /*
            //TODO: check for broken links?
            //always start the share on the first level in the set
            level = level.FindFirstLink();

            string folderName = Utils.FolderNameFromFlags(level.Genres);
            string fullPath = Path.Combine(BokuGame.Settings.MediaPath, folderName, level.WorldId.ToString() + @".Xml");

            // Share.
            // Check to see if the community server is reachable before sharing level.
            if (!Web.Community.Async_Ping(Callback_Ping, fullPath))
            {
                ShowNoCommunityDialog();
            }
            */
        }   // end of ContinueCommunityShare()

        public void LoadContent(bool immediate)
        {
        }   // end of LoadContent()

        public void InitDeviceResources(GraphicsDevice device)
        {
        }

        public void UnloadContent()
        {
        }   // end of UnloadContent()

        public void Render()
        {
            if (!Active)
            {
                return;
            }

            InGame.RenderMessages();//Needed??
        }

        private enum States
        {
            Inactive,
            Active,
        }
        private States state = States.Inactive;

        public bool Active
        {
            get { return (state == States.Active); }
        }

        public override bool Refresh(List<UpdateObject> updateList, List<RenderObject> renderList)
        {
            Debug.Assert(false, "This object is not designed to be put into any lists.");
            return true;
        }   // end of Refresh()

        override public void Activate()
        {
            if (state != States.Active)
            {
                state = States.Active;
                BokuGame.objectListDirty = true;
            }
        }

        public void Activate(LevelMetadata level)
        {
            CurWorld = level;
            PopupOnCommunityShare(level);
            Activate();
        }
        override public void Deactivate()
        {
            state = States.Inactive;
        }   // End of Deactivate()

    }   // end of class 

}   // end of namespace Boku

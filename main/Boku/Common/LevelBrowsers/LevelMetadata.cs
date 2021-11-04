// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.

using System;
using System.Collections.Generic;
using System.Diagnostics;



using Boku.Common.Xml;

using BokuShared;
using BokuShared.Wire;

namespace Boku.Common
{
    /// <summary>
    /// A record containing the metadata for one level, as well as some status flags.
    /// </summary>
    public class LevelMetadata : IDisposable
    {
        private bool disposed;

        ~LevelMetadata()
        {
            Dispose();
        }

        public void Dispose()
        {
            if (!disposed)
            {
                disposed = true;
                Thumbnail.Dispose();
            }
        }

        public enum DownloadStates
        {
            None,
            Queued,
            InProgress,
            Complete,
            Failed
        }

        public DownloadStates DownloadState = DownloadStates.None;

        private ILevelBrowser browser;
        public ILevelBrowser Browser
        {
            get { return browser; }
            set { Debug.Assert(browser == null || value == null); browser = value; }
        }

        /*
            Clarification on what the various time values are...
         
            In SQL we have:
            •	Created : Time level was originally uploaded to SQL.  Not used for anything as far as I can tell.
            •	Modified : When this entry was last written to in the database.  This gets modified on update.  This is used 
                    for sorting levels by date.  Note that even for levels that have not been modified this is often slightly 
                    different than Created.  Apparently we are using DataTime.Now for both and so we get fractionally different 
                    times for each when a level is first uploaded.
            •	LastWriteTime : This is the time that is used for creating the checksum and comes from the uploaded metadata.

            In the Client:
            •	LastSaveTime : This is the same as SQL's LastWriteTime.  Updated on write and then used for checksum calculation.
            •	LastWriteTime : This gets SQL's Modified time.  Used for sorting by date when in the Community browser.  In the 
                    local browser we use the system file write time to sort on so this will have invalid data in it.  This only has 
                    valid data when it gets it from the Community server.

            All these times are UTC.  

            The C# DateTime class keeps track of whether or not a time is UTC but SQL doesn't so we have to coerce all DateTimes 
            we get from SQL into UTC every time we get a DateTime back from the database.  This is done through the SpecifyKind()
            method which forces the UTC flag to be set without changing the time value.  
                LastWriteTime = DateTime.SpecifyKind(packet.Modified, DateTimeKind.Utc);
                LastSaveTime = DateTime.SpecifyKind(packet.LastSaveTime, DateTimeKind.Utc);

            In SQL we need to use DateTime2 instead of DateTime as the type since SQL's DateTime has less precision than .Net's DateTime class.
        */

        /// <summary>
        /// Level metadata fields
        /// </summary>
        public Guid WorldId;
        public DateTime LastWriteTime = DateTime.MinValue;
        public string Name;
        public string Description;
        public UI2D.UIGridElement.Justification DescJustification = Boku.UI2D.UIGridElement.Justification.Left;
        public string Creator;
        public Genres Genres;
        public int VotesUp;
        public int VotesDown;
        public int Downloads;

        public int NumLikes;
        public bool LikedByThisUser = false;    // Just here to let us fake preventing more than one like.
        public int NumComments;
        public string Permalink;

        public Guid? LinkedFromLevel;
        public Guid? LinkedToLevel;

        public byte[] ThumbnailBytes;
        public AsyncThumbnail Thumbnail = new AsyncThumbnail();

        public SoclCommentPacket[] CommentDetails;
        public string RowKey;
        public string PartitionKey;

        public string ThumbnailUrl;
        public string DataUrl;

        public string Checksum;

        // Generated fields used within the UI
        public TextBlob UIDescBlob = null;
        public List<string> UIName = new List<string>();

        // ILevelBrowser's associated state
        public object BrowserState = null;

        /// <summary>
        /// The user's current vote on this level, if any.
        /// </summary>
        public Vote MyVote;

        /// <summary>
        /// True if user has flagged this level.
        /// </summary>
        public bool FlaggedByMe;

        /// <summary>
        /// Returns a computed value based on the number of up/down votes.
        /// </summary>
        public float Rating
        {
            get
            {
                float diff = VotesUp - VotesDown;
                float total = VotesUp + VotesDown;
                return diff * total;
            }
        }

        public DateTime LastSaveTime = DateTime.MinValue;   //Used to determine if level is owned by user. 
                                                            //Note it should usually be the same as lastWriteTime but not the same as Modified.
        internal LevelMetadata Duplicate()
        {
            LevelMetadata level = new LevelMetadata();
            level.WorldId = WorldId;
            level.LastWriteTime = LastWriteTime;
            level.Name = Name;
            level.Description = Description;
            level.Creator = Creator;
            level.Genres = Genres;
            level.VotesUp = VotesUp;
            level.VotesDown = VotesDown;
            level.Downloads = Downloads;
            level.NumLikes = NumLikes;
            level.NumComments = NumComments;
            level.Permalink = Permalink;
            level.Thumbnail = Thumbnail;
            level.CommentDetails = CommentDetails;

            level.LinkedFromLevel = LinkedFromLevel;
            level.LinkedToLevel = LinkedToLevel;

            level.RowKey = RowKey;
            level.PartitionKey = PartitionKey;
            level.Checksum = Checksum;
            level.LastSaveTime = LastSaveTime;

            level.ThumbnailUrl = ThumbnailUrl;
            level.DataUrl = DataUrl;

            return level;
        }

        public void FromPacket(WorldInfoPacket packet)
        {
            WorldId = packet.WorldId;
            LastWriteTime = DateTime.SpecifyKind(packet.Modified, DateTimeKind.Utc);
            Name = packet.Name;
            Description = packet.Description;
            Creator = packet.Creator;
            Genres = (Genres)packet.Genres;
            VotesUp = packet.VotesUp;
            VotesDown = packet.VotesDown;
            Downloads = packet.Downloads;
            NumLikes = packet.Likes;
            NumComments = packet.Comments;
            Permalink = packet.Permalink;
            MyVote = (Vote)packet.MyVote;
            FlaggedByMe = packet.FlaggedByMe;
            ThumbnailBytes = packet.ThumbnailBytes;
            CommentDetails = packet.CommentDetails;
            RowKey = packet.RowKey;
            PartitionKey = packet.PartitionKey;
            Checksum = packet.checksum;
            LastSaveTime = DateTime.SpecifyKind(packet.LastSaveTime, DateTimeKind.Utc);

            // ThumbnailUrl =
            // DataUrl = 
        }

        public void ToPacket(WorldInfoPacket packet)
        {
            packet.WorldId = WorldId;
            packet.Created = packet.Modified = LastWriteTime;
            packet.Name = Name;
            packet.Description = Description;
            packet.Creator = Creator;
            packet.Genres = (int)(Genres & ~Genres.Virtual);
            packet.VotesUp = VotesUp;
            packet.VotesDown = VotesDown;
            packet.Downloads = Downloads;
            packet.Likes = NumLikes;
            packet.Comments = NumComments;
            packet.Permalink = Permalink;
            packet.MyVote = (int)MyVote;
            packet.FlaggedByMe = FlaggedByMe;
            packet.ThumbnailBytes = ThumbnailBytes;
            packet.CommentDetails = CommentDetails;
            packet.RowKey = RowKey;
            packet.PartitionKey = PartitionKey;
            packet.LastSaveTime = LastSaveTime;
            // packet.ThumbnailUrl = ThumbnailUrl;
            // packet.DataUrl = DataUrl;
        }

        public void FromXml(XmlWorldData xml)
        {
            WorldId = xml.id;
            LastWriteTime = xml.lastWriteTime;
            Name = xml.name;
            Description = xml.description;
            DescJustification = xml.descJustification;
            Creator = xml.creator;
            Checksum = xml.checksum;

            Genres = (Genres)xml.genres;
            VotesUp = 0;
            VotesDown = 0;
            Downloads = 0;
            NumLikes = 0;
            NumComments = 0;
            Permalink = null;
            MyVote = Vote.None;
            FlaggedByMe = false;
            ThumbnailBytes = null;

            LinkedFromLevel = xml.LinkedFromLevel;
            LinkedToLevel = xml.LinkedToLevel;
            LastSaveTime = xml.lastWriteTime;

            // ThumbnailUrl = xml.ThumbnailUrl;
            // DataUrl = xml.DataUrl;
        }

        public void ToXml(XmlWorldData xml)
        {
            xml.id = WorldId;
            xml.lastWriteTime = LastWriteTime;
            xml.name = Name;
            xml.description = Description;
            xml.creator = Creator;
            xml.checksum = Checksum;
            xml.genres = (int)(Genres & ~(Genres.Virtual));
            xml.LinkedFromLevel = LinkedFromLevel;
            xml.LinkedToLevel = LinkedToLevel;

            // xml.ThumbnailUrl = ThumbnailUrl;
            // xml.DataUrl = DataUrl;
        }

        /// <summary>
        /// Finds and returns the next linked level in the chain.
        /// </summary>
        /// <returns>Metadata if valid, null if no link or file for link not found.</returns>
        public LevelMetadata NextLink()
        {
            if (LinkedToLevel != null && XmlDataHelper.CheckWorldExistsByGenre((Guid)LinkedToLevel, Genres))
            {
                LevelMetadata level = XmlDataHelper.LoadMetadataByGenre((Guid)LinkedToLevel, Genres);

                // Make sure the link is consistent or don't return it.
                if (level != null && level.LinkedFromLevel == this.WorldId)
                {
                    return level;
                }
            }

            return null;
        }   // end of NextLink()

        /// <summary>
        /// Finds and returns the previous linked level in the chain.
        /// </summary>
        /// <returns>Metadata if valid, null if no link or file for link not found.</returns>
        public LevelMetadata PreviousLink()
        {
            if (LinkedFromLevel != null && XmlDataHelper.CheckWorldExistsByGenre((Guid)LinkedFromLevel, Genres))
            {
                LevelMetadata level = XmlDataHelper.LoadMetadataByGenre((Guid)LinkedFromLevel, Genres);

                // Make sure the link is consistent or don't return it.
                if (level != null && level.LinkedToLevel == this.WorldId)
                {
                    return level;
                }
            }

            return null;
        }   // end of PreviousLink()

        /// <summary>
        /// Finds and returns the first linked level in the chain.
        /// Note this doesn't differentiate between finding the first level and
        /// finding a level with a bad previous link.
        /// </summary>
        /// <returns>Metadata if valid, null if file for link not found.</returns>
        public LevelMetadata FindFirstLink()
        {
            LevelMetadata firstLink = this;
            LevelMetadata previousLink = this;

            // Loop until the previous link is null
            while (previousLink != null)
            {
                firstLink = previousLink;
                previousLink = previousLink.PreviousLink();
            }

            return firstLink;
        }   // end of FindFirstLink()

        /// <summary>
        /// Finds and returns the last linked level in the chain.
        /// Note this doesn't differentiate between finding the last level and
        /// finding a level with a bad previous link.
        /// </summary>
        /// <returns>Metadata if valid, null if file for link not found.</returns>
        public LevelMetadata FindLastLink()
        {
            LevelMetadata lastLink = this;
            LevelMetadata nextLink = this;

            while (nextLink != null)
            {
                lastLink = nextLink;
                nextLink = nextLink.NextLink();
            }

            return lastLink;
        }   // end of FindLastLink()

        /// <summary>
        /// Calculates the total number of levels in the chain.
        /// </summary>
        /// <returns></returns>
        public int CalculateTotalLinkLength()
        {
            LevelMetadata firstLink = FindFirstLink();

            int linkLength = 1;
            while (null != firstLink && firstLink.LinkedToLevel != null)
            {
                firstLink = firstLink.NextLink();
                linkLength++;
            }

            return linkLength;
        }   // end of CalculateTotalLinkLength()

        /// <summary>
        /// Traverses the linked level chain verifying that all previous and next
        /// links are properly connected.
        /// 
        /// TODO (scoy) This could be both cleaner and more complete.  Right now, the Previous links
        /// are only validated for going back to the first in the chain.  From the tail to the current
        /// level they are ignored.  Also, the NextLink() and PreviousLink() functions don't 
        /// differentiate between null links and links to invalid files.  Links to invalid files should
        /// probably be removed with a warnign to the user.
        /// Rewrite this when adding arbitrary links.
        /// </summary>
        /// <param name="brokenLevel">The level where the broken link was found.</param>
        /// <param name="forwardLink">True if the broken link was Next.  False if the broken link was Previous.</param>
        /// <returns>True if broken, false if ok.</returns>
        public bool FindBrokenLink(ref LevelMetadata brokenLevel, ref bool forwardLink)
        {
            // Initialize the out params - assume no broken link.
            brokenLevel = null;
            forwardLink = false;

            // First, walk backwards to the first link.
            LevelMetadata currentLink = this;
            LevelMetadata previousLink = null;
            LevelMetadata nextLink = null;

            while (currentLink.LinkedFromLevel != null)
            {
                // Check to make sure the xml exists.
                previousLink = currentLink.PreviousLink();

                if (null == previousLink)
                {
                    brokenLevel = currentLink;
                    forwardLink = false; // Broke walking backwards.

                    return true;
                }
                currentLink = previousLink;
            }

            // First link now points to the beginning, walk forward to the end.
            while (currentLink.LinkedToLevel != null)
            {
                // Check to make sure the xml exists.
                nextLink = currentLink.NextLink();

                if (null == nextLink)
                {
                    brokenLevel = currentLink;
                    forwardLink = false;    // Broke walking forwards.

                    return true;
                }
                currentLink = nextLink;

            }

            // If we made it this far, all of the links worked out.
            return false;

        }   // end of FindBrokenLink()

        public static LevelMetadata CreateFromXml(XmlWorldData xml)
        {
            LevelMetadata metadata = new LevelMetadata();
            metadata.FromXml(xml);
            return metadata;
        }
    }

    /// <summary>
    /// Contains one page of level metadata.
    /// </summary>
    public class LevelMetadataPage
    {
        public int First;
        public int Total;
        public List<LevelMetadata> Listing = new List<LevelMetadata>();
    }
}

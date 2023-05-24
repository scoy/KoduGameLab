// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.


using System;
using System.Diagnostics;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework.Storage;


using Boku.Common.Sharing;

namespace Boku
{
    partial class BokuGame
    {
        public static bool syncRefresh = false;
        public static PresentInterval presentInterval = PresentInterval.Two;

        /// <summary>
        /// Prevent the system from clearing the backbuffer each frame.
        /// </summary>
        public void PreparingDeviceSettingsHandler(object sender, EventArgs e)
        {
            PreparingDeviceSettingsEventArgs args = e as PreparingDeviceSettingsEventArgs;
            if (args != null)
            {
                Microsoft.Xna.Framework.GraphicsDeviceInformation info = args.GraphicsDeviceInformation;
                info.PresentationParameters.RenderTargetUsage = RenderTargetUsage.PlatformContents;

                /*
                // PresentInterval.Two only works in full screen mode.
                if (BokuSettings.Settings.FullScreen)
                {
                    info.PresentationParameters.PresentationInterval = presentInterval;
                }
                */

            }
        }

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            //
            // Graphics 
            //

            // Determine if HiDef is supported.
            // Find the default adapter and check if it supports Reach and Hidef.
            // TODO (****) What do we do if Reach isn't supported???
            foreach (GraphicsAdapter ga in GraphicsAdapter.Adapters)
            {
                if (ga.IsDefaultAdapter)
                {
                    if (ga.IsProfileSupported(GraphicsProfile.Reach))
                    {
                        hwSupportsReach = true;
                    }
                    if (ga.IsProfileSupported(GraphicsProfile.HiDef))
                    {
                        hwSupportsHiDef = true;
                    }

                    break;
                }
            }

            // Set HiDef iff HW supports AND user doesn't prefer Reach.
            hidef = false;
            if (hwSupportsHiDef && !BokuSettings.Settings.PreferReach)
            {
                hidef = true;
            }
            else
            {
                BokuSettings.ConstrainToReach();
            }

            Debug.Assert(false, "Should we even be here?");
            
            // Select right profile.
            graphics.GraphicsProfile = BokuGame.HiDefProfile ? GraphicsProfile.HiDef : GraphicsProfile.Reach;

            //graphics.PreferredBackBufferFormat = SurfaceFormat.Color;
            graphics.PreferredDepthStencilFormat = DepthFormat.Depth24Stencil8;

            // <<<<<<<<<<<<<<<<<<<<<<<<<<<<<< FULL SCREEN WINDOWED MODE FIX
            
            // Always start windowed.
            graphics.IsFullScreen = false;
            
            // FULL SCREEN WINDOWED MODE FIX >>>>>>>>>>>>>>>>>>>>>>>>>>>>>

            graphics.SynchronizeWithVerticalRetrace = syncRefresh;
            graphics.PreferMultiSampling = BokuSettings.Settings.AntiAlias;

            //
            // Game
            //

            IsMouseVisible = true;

        }   // end of BokuGame InitializeComponent()

        GraphicsDeviceManager graphics = null;

    }   // end of partial class BokuGame

}   // end of namespace Boku

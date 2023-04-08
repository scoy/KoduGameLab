// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.


using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework.Storage;

using Boku.Common;
using Boku.Input;

namespace Boku
{
    /// <summary>
    /// This is an object which manages the top level lists
    /// of objects in the game.  There will only be one of
    /// these objects.
    /// </summary>
    public class GameListManager
    {
        // TODO (****) Try and change these to be more type specific.
        public List<object> objectList = null;
        public List<UpdateObject> updateList = null;
        public List<RenderObject> renderList = null;
        public List<object> collideList = null;

        TextBlob blob;
        Texture2D deadKoduTexture;

        // c'tor
        public GameListManager()
        {
            objectList = new List<object>();
            updateList = new List<UpdateObject>();
            renderList = new List<RenderObject>();
            collideList = new List<object>();

        }   // end of GameListManager c'tor


        public void AddObject(GameObject obj)
        {
            objectList.Add(obj);
            BokuGame.objectListDirty = true;
        }   // end of GameListManager AddObject()


        public void RemoveObject(GameObject obj)
        {
            objectList.Remove(obj);
            BokuGame.objectListDirty = true;
        }   // end of GameListManager RemoveObject()


        public void Refresh()
        {
            for (int i = 0; i < objectList.Count; ++i)
            {
                GameObject obj = (GameObject)objectList[i];
                if (obj.Refresh(updateList, renderList) && objectList.Count > 0)
                {
                    --i;
                }
            }
        }   // end of GameListManager Refresh()


        public void Update()
        {
            // Lazy allocation
            if (blob == null)
            {
                blob = new TextBlob(UI2D.Shared.GetGameFont20, Strings.Localize("mainMenu.paused"), 300);
                blob.Justification = UI2D.UIGridElement.Justification.Center;
                deadKoduTexture = BokuGame.Load<Texture2D>(BokuGame.Settings.MediaPath + @"Textures\SleepyKodu");
            }

            // Check if microbit:driver needs installing.
            if (MicrobitManager.DriverInstalled == false)
            {
                MicrobitManager.ShowDriverDialog();
            }

            for (int i = 0; i < updateList.Count; ++i)
            {
                UpdateObject obj = updateList[i] as UpdateObject;
                obj.Update();
            }
        }   // end of GameListManager Update()


        public void Render()
        {
            for (int i = 0; i < renderList.Count; ++i)
            {
                RenderObject obj = renderList[i] as RenderObject;
                obj.Render(null);
            }
        }   // end of GameListManager Render()


    }   // end of class GameListManager

}   // end of namespace Boku

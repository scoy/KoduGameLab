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
using TileProcessor;

namespace Boku.Base
{
    public class ControlRenderObj : RenderObject, ITransform, IBounding
    {
        private ClassRenderObj classRenderObj;
        private StateRenderObj staticRender;
        private StateRenderObj activeRender;
        private Transform localTransform = new Transform();
        private Matrix worldMatrix;

        private Object parent;
        public List<RenderObject> renderList = new List<RenderObject>();

        public const string idStateNormal = "normal:";
        public const string idStateHot = "hot:";
        public const string idStateDisabled = "disabled:";
        public const string idStateSelected = "selected:";

        public List<List<PartInfo>> listStaticPartInfos = null;
        public List<List<PartInfo>> listActivePartInfos = null;

        public ControlRenderObj(Object parent, ClassRenderObj classRenderObj)
        {
            this.parent = parent;
            /*
                        ITransform transformThis = this as ITransform;
                        transformThis.Local = Matrix.Identity;
            */
            this.classRenderObj = classRenderObj;
            this.staticRender = classRenderObj.GetStateRenderObj(ClassRenderObj.StaticStateName);
        }
        public Object Parent
        {
            get
            {
                return this.parent;
            }
            set
            {
                this.parent = value;

                RecalcMatrix();
            }
        }
        protected void RecalcMatrix()
        {
            ITransform transformParent = this.parent as ITransform;
            ITransform transformThis = this as ITransform;
            Matrix parentMatrix = transformParent.World;
            transformThis.Recalc(ref parentMatrix);
        }

        public List<List<PartInfo>> ListStaticPartInfos
        {
            get
            {
                if (staticRender != null)
                {
                    return staticRender.ListPartInfo;
                }
                return null;
            }
        }
        public List<List<PartInfo>> ListActivePartInfos
        {
            get
            {
                if (activeRender != null)
                {
                    return activeRender.ListPartInfo;
                }
                return null;
            }
        }
        public List<List<PartInfo>> ListStatePartInfos(string stateId)
        {
            StateRenderObj stateRender = classRenderObj.GetStateRenderObj(stateId);
            if (stateRender != null)
            {
                return stateRender.ListPartInfo;
            }
            return null;
        }
        BoundingBox IBounding.BoundingBox
        {
            get
            {
                return this.classRenderObj.BoundingBox;
            }
        }
        BoundingSphere IBounding.BoundingSphere
        {
            get
            {
                return this.classRenderObj.BoundingSphere;
            }
        }
        Transform ITransform.Local
        {
            get
            {
                return this.localTransform;
            }
            set
            {
                this.localTransform = value;
            }
        }
        Matrix ITransform.World
        {
            get
            {
                return this.worldMatrix;
            }
        }
        bool ITransform.Compose()
        {
            bool changed = this.localTransform.Compose();
            if (changed)
            {
                RecalcMatrix();
            }
            return changed;
        }
        void ITransform.Recalc(ref Matrix parentMatrix)
        {

            this.worldMatrix = /*classRenderObj.Transform * */ this.localTransform.Matrix * parentMatrix;

            ITransform transformTarget;

            for (int iChild = 0; iChild < renderList.Count; iChild++)
            {
                transformTarget = renderList[iChild] as ITransform;
                if (transformTarget != null)
                {
                    transformTarget.Recalc(ref this.worldMatrix);
                }
            }
        }
        ITransform ITransform.Parent
        {
            get
            {
                return Parent as ITransform;
            }
            set
            {
                Parent = value;
            }
        }
        public string State
        {
            set
            {
                this.activeRender = this.classRenderObj.GetStateRenderObj(value);
            }
        }
        public bool GetPositionTransform(string position, out Matrix matrix)
        {
            return GetPositionTransform(position, out matrix, false);
        }

        public bool GetPositionTransform(string position, out Matrix matrix, bool inverted)
        {
            Matrix fullTransform;
            matrix = Matrix.Identity;

            if (classRenderObj.GetPositionTransform(position, out fullTransform))
            {
                if (inverted)
                {
                    // just translation for now
                    //matrix = -fullTransform;
                    matrix.Translation = -fullTransform.Translation;
                }
                else
                {
                    // just translation for now
                    //matrix = fullTransform;
                    matrix.Translation = fullTransform.Translation;
                }
                return true;
            }
            return false;
        }

        public override void Render(Camera camera)
        {
            ITransform transformThis = this as ITransform;
            Matrix renderTransform = classRenderObj.Transform * transformThis.World;
            if (staticRender != null)
            {
                staticRender.Render(camera, renderTransform, listStaticPartInfos);
            }
            if (activeRender != null)
            {
                activeRender.Render(camera, renderTransform, listActivePartInfos);
            }

            for (int iChild = 0; iChild < renderList.Count; iChild++)
            {
                RenderObject renderChild = renderList[iChild] as RenderObject;
                renderChild.Render(camera);
            }
        }
        public override void Activate()
        {
            for (int iChild = 0; iChild < renderList.Count; iChild++)
            {
                RenderObject controlChild = renderList[iChild] as RenderObject;
                controlChild.Activate();
            }
        }
        public override void Deactivate()
        {
            for (int iChild = 0; iChild < renderList.Count; iChild++)
            {
                RenderObject controlChild = renderList[iChild] as RenderObject;
                controlChild.Deactivate();
            }
        }
    }

    public class ClassRenderObj
    {
        private ModelBone boneClass;
        private BoundingBox boundingBox;
        private BoundingBox boundingFrame;
        private BoundingSphere boundingSphere;

        public const string StaticStateName = "static:";
        public const string PositionsStateName = "positions:";
        public const string IgnoreStateName = "ignore";
        public const string BoxStateName = "box:";

        private Dictionary<String, StateRenderObj> collection = new Dictionary<String, StateRenderObj>();
        private Dictionary<String, Matrix> positions = new Dictionary<String, Matrix>();


        public ClassRenderObj(string classname, Model model, ModelBone bone)
        {
            this.boneClass = bone;

            foreach (ModelBone boneChild in bone.Children)
            {
                if (!boneChild.Name.StartsWith(IgnoreStateName))
                {
                    if (boneChild.Name.StartsWith(PositionsStateName))
                    {
                        AddPositions(boneChild);
                    }
                    else if (boneChild.Name.StartsWith(BoxStateName))
                    {
                        AddBoundingFrame(model, boneChild);
                    }
                    else
                    {
                        AddStateRender(model, boneChild);
                    }
                }
            }
            this.boundingSphere = CalcBoundingSphere();
            this.boundingBox = CalcBoundingBox();

            // Get the values for the following hack.
            //Debug.Print("case \"" + bone.Name + "\":");
            //Debug.Print("    boundingSphere = new BoundingSphere(new Vector3(" + boundingSphere.Center.X.ToString() + "f, " + boundingSphere.Center.Y.ToString() + "f, " + boundingSphere.Center.X.ToString() + "f), " + boundingSphere.Radius.ToString() + "f);");
            //Debug.Print("    boundingBox = new BoundingBox(new Vector3(" + boundingBox.Min.X.ToString() + "f, " + boundingBox.Min.Y.ToString() + "f, " + boundingBox.Min.X.ToString() + "f), new Vector3(" + boundingBox.Max.X.ToString() + "f, " + boundingBox.Max.Y.ToString() + "f, " + boundingBox.Max.X.ToString() + "f));");
            //Debug.Print("    break;");

        }
        public BoundingBox BoundingBox
        {
            get
            {
                return boundingBox;
            }
        }
        public BoundingBox BoundingFrame
        {
            get
            {
                return boundingFrame;
            }
        }
        public BoundingSphere BoundingSphere
        {
            get
            {
                return boundingSphere;
            }
        }
        protected void AddBoundingFrame(Model model, ModelBone bone)
        {
            if (bone.Children.Count > 0)
            {
                ModelBone boneChild = bone.Children[0];

                // we really only support one mesh as the box
                ModelMesh mesh = ModelHelper.FindMatchingMeshForBone(model, boneChild);

                // include upto panel transform as
                // we want them relative to the panels parent
                Matrix boxTransform = boneChild.Transform * bone.Transform * boneClass.Transform;

                // for now just use an empty one until we have a content pipeline solution
                // BoundingBox box = ModelHelper.CalculateBoundingBox(mesh);
                BoundingBox box;
                UIMeshData data = mesh.Tag as UIMeshData;
                Debug.Assert(data != null, "Missing bounding box on mesh " + mesh.Name + ". Set the content processor on your model to UIModelProcessor.");

                if (data != null)
                {
                    box = data.bBox;
                }
                else
                {
                    box = new BoundingBox();
                }

                // apply transform to min and max and create a new aligned box
                Vector3[] points = new Vector3[2];
                points[0] = Vector3.Transform(box.Min, boxTransform);
                points[1] = Vector3.Transform(box.Max, boxTransform);

                this.boundingFrame = BoundingBox.CreateFromPoints(points);
            }
        }

        protected void AddPositions(ModelBone bone)
        {
            foreach (ModelBone bonePosition in bone.Children)
            {
                // if position names get decorated with unique ids (like _ncls1_4 ) then trim them
                //
                int indexTrim = bonePosition.Name.IndexOf('_');
                string positionName;
                if (indexTrim != -1)
                {
                    positionName = bonePosition.Name.Remove(indexTrim, bonePosition.Name.Length - indexTrim).Trim();
                }
                else
                {
                    positionName = bonePosition.Name.Trim();
                }
                // include the positions containers transforms but ignore the panel
                // as we want them relative to the panel
                Matrix positionTransform = bonePosition.Transform * bone.Transform * bone.Parent.Transform;

                //                ModelHelper.DebugOutTransform(bonePosition.Name, positionTransform);
                positions.Add(positionName, positionTransform);
            }
        }

        protected void AddStateRender(Model model, ModelBone bone)
        {
            string controlName;
            int indexSplit = bone.Name.IndexOf(":");
            if (indexSplit > 0)
            {
                controlName = bone.Name.Substring(0, indexSplit + 1).Trim();
            }
            else
            {
                controlName = bone.Name.Trim();
            }

            // create a state render object
            StateRenderObj stateRender = new StateRenderObj(model, bone);
            collection.Add(controlName, stateRender);
        }
        protected BoundingSphere CalcBoundingSphere()
        {
            BoundingSphere sphere = new BoundingSphere();
            IEnumerator<StateRenderObj> enumStates = collection.Values.GetEnumerator();
            enumStates.Reset();
            while (enumStates.MoveNext())
            {
                sphere = BoundingSphere.CreateMerged(sphere, enumStates.Current.BoundingSphere);
            }
            return sphere;
        }
        protected BoundingBox CalcBoundingBox()
        {
            BoundingBox box = new BoundingBox();
            IEnumerator<StateRenderObj> enumStates = collection.Values.GetEnumerator();
            enumStates.Reset();
            while (enumStates.MoveNext())
            {
                box = BoundingBox.CreateMerged(box, enumStates.Current.BoundingBox);
            }
            return box;
        }

        public Matrix Transform
        {
            get
            {
                return boneClass.Transform;
            }
        }
        public StateRenderObj GetStateRenderObj(string state)
        {
            StateRenderObj render = null;
            collection.TryGetValue(state, out render);
            return render;
        }

        public bool GetPositionTransform(string position, out Matrix matrix)
        {
            return positions.TryGetValue(position, out matrix);
        }
    }

    public class StateRenderObj
    {
        // TODO (****) make more specific.
        private List<object> listMeshRenderObj;
        private BoundingSphere boundingSphere;
        private BoundingBox boundingBox;

        public StateRenderObj(Model model, ModelBone bone)
        {
            listMeshRenderObj = new List<object>();
            RecurseBonesAndCreateRenderObjs(model, bone);
            this.boundingSphere = CalcBoundingSphere();
            this.boundingBox = CalcBoundingBox();
        }

        public BoundingSphere BoundingSphere
        {
            get
            {
                return boundingSphere;
            }
        }

        public BoundingBox BoundingBox
        {
            get
            {
                return boundingBox;
            }
        }

        public List<List<PartInfo>> ListPartInfo
        {
            get
            {
                // TODO (****) make more specific.
                List<List<PartInfo>> listPartInfos = new List<List<PartInfo>>();
                for (int iMesh = 0; iMesh < listMeshRenderObj.Count; iMesh++)
                {
                    MeshRenderObj meshRenderObj = (MeshRenderObj)listMeshRenderObj[iMesh];
                    List<PartInfo> listPartInfo = meshRenderObj.ListPartInfo;
                    List<PartInfo> listPartInfoCopy = new List<PartInfo>();
                    for (int iPart = 0; iPart < listPartInfo.Count; iPart++)
                    {
                        PartInfo copy = new PartInfo(listPartInfo[iPart]);
                        listPartInfoCopy.Add(copy);
                    }
                    listPartInfos.Add(listPartInfoCopy);
                }
                return listPartInfos;
            }
        }
        private void RecurseBonesAndCreateRenderObjs(Model model, ModelBone bone)
        {
            foreach (ModelBone childbone in bone.Children)
            {
                ModelMesh mesh = ModelHelper.FindMatchingMeshForBone(model, childbone);
                if (mesh != null)
                {
                    List<PartInfo> meshInfoList = new List<PartInfo>();
                    foreach (ModelMeshPart part in mesh.MeshParts)
                    {
                        PartInfo partInfo = new PartInfo();
                        partInfo.InitFromPart(part);
                        meshInfoList.Add(partInfo);
                        // create a bounding box from the mesh?
                    }
                    MeshRenderObj renderobj = new MeshRenderObj(mesh, meshInfoList);
                    listMeshRenderObj.Add(renderobj);
                }
                RecurseBonesAndCreateRenderObjs(model, childbone);
            }
        }
        protected BoundingSphere CalcBoundingSphere()
        {
            BoundingSphere sphere = new BoundingSphere();
            for (int iMesh = 0; iMesh < listMeshRenderObj.Count; iMesh++)
            {
                MeshRenderObj meshRenderObj = listMeshRenderObj[iMesh] as MeshRenderObj;
                sphere = BoundingSphere.CreateMerged(sphere, meshRenderObj.BoundingSphere);
            }
            return sphere;
        }
        protected BoundingBox CalcBoundingBox()
        {
            BoundingBox box = new BoundingBox();
            for (int iMesh = 0; iMesh < listMeshRenderObj.Count; iMesh++)
            {
                MeshRenderObj meshRenderObj = listMeshRenderObj[iMesh] as MeshRenderObj;
                box = BoundingBox.CreateMerged(box, meshRenderObj.BoundingBox);
            }
            return box;
        }
        public void Render(Camera camera, Matrix transform, List<List<PartInfo>> listMeshPartInfos)
        {
            Debug.Assert(listMeshPartInfos == null || listMeshRenderObj.Count == listMeshPartInfos.Count);
            for (int iMesh = 0; iMesh < listMeshRenderObj.Count; iMesh++)
            {
                MeshRenderObj meshRenderObj = (MeshRenderObj)listMeshRenderObj[iMesh];
                List<PartInfo> listPartInfo = null;
                if (listMeshPartInfos != null)
                {
                    listPartInfo = listMeshPartInfos[iMesh];
                }
                meshRenderObj.Render(camera, transform, listPartInfo);
            }
        }
    }
    public class MeshRenderObj
    {
        private ModelMesh mesh;
        // list of PartInfo for each part.
        private List<PartInfo> listPartInfo;
        private Matrix meshTransform;

        public List<PartInfo> ListPartInfo
        {
            get
            {
                return listPartInfo;
            }
        }
        public MeshRenderObj(ModelMesh mesh, List<PartInfo> listPartInfo)
        {
            this.mesh = mesh;
            this.listPartInfo = listPartInfo;

            meshTransform = mesh.ParentBone.Transform;
            ModelBone parentBone = mesh.ParentBone.Parent;
            // ignore the root bone as it has an invalid/strange transform
            // also ignore the first child of the root as that transform will be
            // applied at render and represents a changeable/default value
            while (parentBone != null &&
                    parentBone.Parent != null &&
                    parentBone.Parent.Parent != null)
            {
                meshTransform = meshTransform * parentBone.Transform;
                parentBone = parentBone.Parent;
            }
            /*
                        UIMeshData data = mesh.Tag as UIMeshData;
                        if (data != null)
                        {
                            System.Diagnostics.Debug.WriteLine("Got BBox min " + data.bBox.Min + " max " + data.bBox.Max + " Object: " + mesh.Name);
                        }

                        // output the results
                        ModelHelper.DebugOutTransform(mesh.ParentBone.Name, meshTransform);
             */
        }
        public BoundingSphere BoundingSphere
        {
            get
            {
                // transform the sphere
                Vector3 center = Vector3.Transform(mesh.BoundingSphere.Center, meshTransform);
                Vector3 radius = Vector3.TransformNormal(new Vector3(mesh.BoundingSphere.Radius, 0.0f, 0.0f), meshTransform);

                BoundingSphere sphere = new BoundingSphere(center, radius.Length());

                return sphere;
            }
        }

        public BoundingBox BoundingBox
        {
            get
            {
                UIMeshData data = mesh.Tag as UIMeshData;
                BoundingBox box;

                Debug.Assert(data != null, "Missing bounding box on mesh " + mesh.Name + ". Set the content processor on your model to UIModelProcessor.");
                if (data != null)
                {
                    box = data.bBox;
                }
                else
                {
                    box = new BoundingBox();
                }

                // transform the box
                Vector3[] points = box.GetCorners();
                for (int indexPoint = 0; indexPoint < points.Length; indexPoint++)
                {
                    points[indexPoint] = Vector3.Transform(points[indexPoint], meshTransform);
                }
                box = BoundingBox.CreateFromPoints(points);

                return box;
            }
        }

        public void Render(Camera camera, Matrix transform, List<PartInfo> listPartsReplacement)
        {
            Effect effect = Editor.Effect;
            GraphicsDevice device = BokuGame.bokuGame.GraphicsDevice;

            List<PartInfo> listParts = listPartInfo;
            if (listPartsReplacement != null)
            {
                listParts = listPartsReplacement;
            }
            Matrix viewProjMatrix = camera.ViewMatrix * camera.ProjectionMatrix;

            Matrix worldMatrix = meshTransform * transform;

            Matrix worldViewProjMatrix = worldMatrix * viewProjMatrix;

            effect.Parameters["WorldViewProjMatrix"].SetValue(worldViewProjMatrix);
            effect.Parameters["WorldMatrix"].SetValue(worldMatrix);

            //
            //FillMode tempFillMode = device.RenderState.FillMode;

            //device.RasterizerState = UI2D.Shared.RasterStateWireframe;

            for (int indexMeshPart = 0; indexMeshPart < mesh.MeshParts.Count; indexMeshPart++)
            {
                ModelMeshPart part = (ModelMeshPart)mesh.MeshParts[indexMeshPart];

                device.Indices = part.IndexBuffer;
                device.SetVertexBuffer(part.VertexBuffer);

                // Apply part info params.
                PartInfo partInfo = listParts[indexMeshPart];

                if (partInfo.DiffuseTexture != null)
                {
                    // TODO This is just a hack to keep things from breaking.  The real issue
                    // here is that we need to rebuild the ReflexHandle line number cache
                    // textures when the device is lost and at this time I'm not real sure
                    // how to get there from here...
                    // mattmac: need to test if overlaytexture exists                    bool staleTextures = partInfo.OverlayTexture.GraphicsDevice.IsDisposed || partInfo.OverlayTexture.IsDisposed;
                    bool noOverlay = partInfo.OverlayTexture == null
                        || partInfo.OverlayTexture.GraphicsDevice.IsDisposed
                        || partInfo.OverlayTexture.IsDisposed;

                    if (noOverlay)
                    {
                        effect.Parameters["OverlayTexture"].SetValue(partInfo.DiffuseTexture);      // Foreground texture.
                        effect.CurrentTechnique = effect.Techniques["OneTextureColorPass"];
                    }
                    else
                    {
                        effect.Parameters["OverlayTexture"].SetValue(partInfo.OverlayTexture);      // Foreground texture, ie the icon itself.
                        effect.Parameters["DiffuseTexture"].SetValue(partInfo.DiffuseTexture);      // Background texture, soft glow.

                        effect.CurrentTechnique = effect.Techniques["TwoTextureColorPass"];
                    }
                }
                else
                {
                    if (partInfo.OverlayTexture == null)
                    {
                        effect.CurrentTechnique = effect.Techniques["NoTextureColorPass"];
                    }
                    else
                    {
                        effect.Parameters["OverlayTexture"].SetValue(partInfo.OverlayTexture);      // Foreground texture.
                        effect.CurrentTechnique = effect.Techniques["OneTextureColorPass"];
                    }
                }

                effect.Parameters["DiffuseColor"].SetValue(partInfo.DiffuseColor);
                effect.Parameters["SpecularColor"].SetValue(partInfo.SpecularColor);
                effect.Parameters["EmissiveColor"].SetValue(partInfo.EmissiveColor);
                effect.Parameters["SpecularPower"].SetValue(partInfo.SpecularPower);

                effect.CurrentTechnique.Passes[0].Apply();

                for (int indexPass = 0; indexPass < effect.CurrentTechnique.Passes.Count; indexPass++)
                {
                    EffectPass pass = (EffectPass)effect.CurrentTechnique.Passes[indexPass];
                    pass.Apply();
                    device.DrawIndexedPrimitives(PrimitiveType.TriangleList,
                                                    part.VertexOffset,
                                                    0,
                                                    part.NumVertices,
                                                    part.StartIndex,
                                                    part.PrimitiveCount);
                }
            }
            //device.RenderState.FillMode = tempFillMode;
        }

    }   // end of class MeshRenderObj 
}

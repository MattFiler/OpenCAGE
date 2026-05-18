using CATHODE;
using CATHODE.Scripting;
using CATHODE.Scripting.Internal;
using CathodeLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Windows.Media.Media3D;
using NumericsQuaternion = System.Numerics.Quaternion;
using AlienPAK;

namespace OpenCAGE
{
    /// <summary>
    /// Builds a 3D scene graph from a Commands composite (alias/proxy aware).
    /// </summary>
    public static class CompositeSceneBuilder
    {
        public class SceneNode
        {
            public Entity Entity;
            public Composite OwnerComposite;
            public SceneNode Parent;
            public SceneGraph Graph;
            public Transform3D Transform;
            public TranslateTransform3D Translation;
            public QuaternionRotation3D Rotation;
            public Model3DGroup Content = new Model3DGroup();
            public ModelVisual3D Visual;
            /// <summary>Detached copy of transparent geometry; needs world matrix, not local.</summary>
            public ModelVisual3D TransparentVisual;
            public SceneNode PointedTarget;
            public SceneNode OverrideOwner;
            public bool IsPointedTarget;
        }

        private const double DegToRad = Math.PI / 180.0;
        private const double RadToDeg = 180.0 / Math.PI;

        public class SceneGraph
        {
            public ModelVisual3D Root = new ModelVisual3D();
            public List<SceneNode> Nodes = new List<SceneNode>();
            public Dictionary<ShortGuid, SceneNode> ByEntity = new Dictionary<ShortGuid, SceneNode>();

            public void RefreshTransparentTransforms()
            {
                foreach (SceneNode node in Nodes)
                {
                    if (node.TransparentVisual == null)
                        continue;

                    node.TransparentVisual.Transform = new MatrixTransform3D(GetWorldMatrix3D(node));
                }
            }
        }

        public static SceneGraph Build(LevelContent content, Composite composite)
        {
            SceneGraph graph = new SceneGraph();
            if (content?.Level?.Commands == null || composite == null)
                return graph;

            BuildContext ctx = new BuildContext(content);
            AddCompositeInstance(ctx, graph, composite, null, composite);
            return graph;
        }

        private class BuildContext
        {
            public LevelContent Content;
            public Commands Commands => Content.Level.Commands;

            public BuildContext(LevelContent content)
            {
                Content = content;
            }
        }

        private static void AddCompositeInstance(BuildContext ctx, SceneGraph graph, Composite composite, SceneNode parentNode, Composite ownerComposite)
        {
            if (composite == null)
                return;

            foreach (Entity entity in composite.functions)
                AddEntity(ctx, graph, composite, entity, parentNode, ownerComposite);
            foreach (Entity entity in composite.variables)
                AddEntity(ctx, graph, composite, entity, parentNode, ownerComposite);
            foreach (Entity entity in composite.aliases)
                AddEntity(ctx, graph, composite, entity, parentNode, ownerComposite);
            foreach (Entity entity in composite.proxies)
                AddEntity(ctx, graph, composite, entity, parentNode, ownerComposite);
        }

        private static void AddEntity(BuildContext ctx, SceneGraph graph, Composite ownerComposite, Entity entity, SceneNode parentNode, Composite instanceComposite)
        {
            SceneNode node = CreateNode(ctx, graph, ownerComposite, entity, parentNode);

            if (parentNode != null)
                parentNode.Visual.Children.Add(node.Visual);
            else
                graph.Root.Children.Add(node.Visual);

            switch (entity.variant)
            {
                case EntityVariant.ALIAS:
                    BindPointedEntity(ctx, graph, node, instanceComposite, isProxy: false);
                    break;
                case EntityVariant.PROXY:
                    BindPointedEntity(ctx, graph, node, ctx.Commands.EntryPoints[0], isProxy: true);
                    break;
                case EntityVariant.FUNCTION:
                    FunctionEntity function = (FunctionEntity)entity;
                    if (!function.function.IsFunctionType)
                    {
                        Composite nested = ctx.Commands.GetComposite(function.function);
                        if (nested != null)
                            AddCompositeInstance(ctx, graph, nested, node, ownerComposite);
                    }
                    else if (function.function.AsFunctionType == FunctionType.ModelReference)
                    {
                        AddRenderables(ctx, node, function);
                    }
                    break;
            }
        }

        private static SceneNode CreateNode(BuildContext ctx, SceneGraph graph, Composite ownerComposite, Entity entity, SceneNode parentNode)
        {
            GetEntityTransform(entity, out Vector3 position, out Vector3 rotation);

            SceneNode node = new SceneNode
            {
                Entity = entity,
                OwnerComposite = ownerComposite,
                Parent = parentNode,
                Graph = graph,
                Translation = new TranslateTransform3D(),
                Rotation = new QuaternionRotation3D(),
                Visual = new ModelVisual3D
                {
                    Content = new Model3DGroup(),
                },
            };
            node.Content = (Model3DGroup)node.Visual.Content;
            SetNodeTransform(node, position, rotation);

            graph.Nodes.Add(node);
            if (!graph.ByEntity.ContainsKey(entity.shortGUID))
                graph.ByEntity.Add(entity.shortGUID, node);

            return node;
        }

        private static void BindPointedEntity(BuildContext ctx, SceneGraph graph, SceneNode node, Composite startComposite, bool isProxy)
        {
            EntityPath path = isProxy ? ((ProxyEntity)node.Entity).proxy : ((AliasEntity)node.Entity).alias;
            if (path?.path == null || path.path.Length == 0)
                return;

            List<Tuple<Composite, Entity>> resolved = ctx.Commands.Utils.ResolveAliasOrProxy(path, startComposite);
            if (resolved == null || resolved.Count == 0)
                return;

            SceneNode target = null;
            foreach (Tuple<Composite, Entity> step in resolved)
            {
                SceneNode stepNode;
                if (target == null)
                {
                    if (isProxy || node.Parent == null)
                        stepNode = FindRootNode(graph, step.Item2);
                    else
                        stepNode = FindChildNode(graph, node.Parent, step.Item2);
                }
                else
                {
                    stepNode = FindChildNode(graph, target, step.Item2);
                }

                if (stepNode == null)
                    return;
                target = stepNode;
            }

            node.PointedTarget = target;
            target.OverrideOwner = node;
            target.IsPointedTarget = true;

            if (node.Entity.GetParameter("position") != null && GetEntityTransform(node.Entity, out Vector3 position, out Vector3 rotation))
                ApplyTransformToNode(target, position, rotation);
        }

        private static void ApplyTransformToNode(SceneNode target, Vector3 position, Vector3 rotation)
        {
            SetNodeTransform(target, position, rotation);
        }

        public static void SetNodeTransform(SceneNode node, Vector3 position, Vector3 rotationEuler)
        {
            node.Translation.OffsetX = position.X;
            node.Translation.OffsetY = position.Y;
            node.Translation.OffsetZ = position.Z;
            node.Rotation = CreateRotation(rotationEuler);

            Transform3DGroup transform = new Transform3DGroup();
            transform.Children.Add(new RotateTransform3D(node.Rotation));
            transform.Children.Add(node.Translation);
            node.Transform = transform;
            node.Visual.Transform = transform;
            node.Graph?.RefreshTransparentTransforms();
        }

        private static SceneNode FindChildNode(SceneGraph graph, SceneNode parent, Entity entity)
        {
            if (parent == null || entity == null)
                return null;

            foreach (SceneNode candidate in graph.Nodes)
            {
                if (candidate.Parent == parent && candidate.Entity.shortGUID == entity.shortGUID)
                    return candidate;
            }

            return null;
        }

        private static SceneNode FindRootNode(SceneGraph graph, Entity entity)
        {
            if (entity == null)
                return null;

            foreach (SceneNode candidate in graph.Nodes)
            {
                if (candidate.Parent == null && candidate.Entity.shortGUID == entity.shortGUID)
                    return candidate;
            }

            return null;
        }

        public static Matrix4x4 GetWorldMatrix(SceneNode node)
        {
            if (node == null)
                return Matrix4x4.Identity;

            Matrix4x4 world = Matrix4x4.Identity;
            List<SceneNode> chain = new List<SceneNode>();
            for (SceneNode current = node; current != null; current = current.Parent)
                chain.Add(current);

            chain.Reverse();
            foreach (SceneNode chainNode in chain)
                world *= CreateLocalMatrix(chainNode);

            return world;
        }

        public static void SetNodeTransformFromWorld(SceneNode node, Matrix4x4 worldMatrix)
        {
            if (node == null)
                return;

            Matrix4x4 parentWorld = node.Parent != null ? GetWorldMatrix(node.Parent) : Matrix4x4.Identity;
            if (!Matrix4x4.Invert(parentWorld, out Matrix4x4 parentInverse))
                return;

            Matrix4x4 localMatrix = parentInverse * worldMatrix;
            if (!TryDecomposeMatrix(ToMatrix3D(localMatrix), out Vector3 position, out Vector3 rotation))
                return;

            SetNodeTransform(node, position, rotation);
        }

        public static Matrix3D GetWorldMatrix3D(SceneNode node)
        {
            return ToMatrix3D(GetWorldMatrix(node));
        }

        private static Matrix4x4 CreateLocalMatrix(SceneNode node)
        {
            if (node?.Transform == null)
                return Matrix4x4.Identity;

            return ToMatrix4x4(GetTransformMatrix(node.Transform));
        }

        /// <summary>Local matrix matching Cathode/Instancing (rotation then translation).</summary>
        public static Matrix4x4 CreateLocalMatrixFromEntity(Entity entity)
        {
            GetEntityTransform(entity, out Vector3 position, out Vector3 rotation);
            NumericsQuaternion quaternion = NumericsQuaternion.CreateFromYawPitchRoll(
                rotation.Y * (float)DegToRad,
                rotation.X * (float)DegToRad,
                rotation.Z * (float)DegToRad);
            return Matrix4x4.CreateFromQuaternion(quaternion) * Matrix4x4.CreateTranslation(position);
        }

        private static Matrix3D ToMatrix3D(Matrix4x4 matrix)
        {
            return new Matrix3D(
                matrix.M11, matrix.M12, matrix.M13, matrix.M14,
                matrix.M21, matrix.M22, matrix.M23, matrix.M24,
                matrix.M31, matrix.M32, matrix.M33, matrix.M34,
                matrix.M41, matrix.M42, matrix.M43, matrix.M44);
        }

        private static Matrix4x4 ToMatrix4x4(Matrix3D matrix)
        {
            return new Matrix4x4(
                (float)matrix.M11, (float)matrix.M12, (float)matrix.M13, (float)matrix.M14,
                (float)matrix.M21, (float)matrix.M22, (float)matrix.M23, (float)matrix.M24,
                (float)matrix.M31, (float)matrix.M32, (float)matrix.M33, (float)matrix.M34,
                (float)matrix.OffsetX, (float)matrix.OffsetY, (float)matrix.OffsetZ, (float)matrix.M44);
        }

        private static QuaternionRotation3D CreateRotation(Vector3 eulerDegrees)
        {
            NumericsQuaternion rotation = NumericsQuaternion.CreateFromYawPitchRoll(
                eulerDegrees.Y * (float)DegToRad,
                eulerDegrees.X * (float)DegToRad,
                eulerDegrees.Z * (float)DegToRad);
            return new QuaternionRotation3D(new System.Windows.Media.Media3D.Quaternion(rotation.X, rotation.Y, rotation.Z, rotation.W));
        }

        private static Matrix3D GetTransformMatrix(Transform3D transform)
        {
            if (transform == null)
                return Matrix3D.Identity;

            if (transform is MatrixTransform3D matrixTransform)
                return matrixTransform.Matrix;

            if (transform is TranslateTransform3D translate)
            {
                return new Matrix3D(
                    1, 0, 0, 0,
                    0, 1, 0, 0,
                    0, 0, 1, 0,
                    translate.OffsetX, translate.OffsetY, translate.OffsetZ, 1);
            }

            if (transform is RotateTransform3D rotate)
            {
                Matrix3D matrix = Matrix3D.Identity;
                if (rotate.Rotation is QuaternionRotation3D quaternionRotation)
                    matrix.Rotate(quaternionRotation.Quaternion);
                else if (rotate.Rotation is AxisAngleRotation3D axisRotation)
                    matrix.Rotate(new System.Windows.Media.Media3D.Quaternion(axisRotation.Axis, axisRotation.Angle));
                return matrix;
            }

            if (transform is Transform3DGroup group)
            {
                Matrix3D result = Matrix3D.Identity;
                foreach (Transform3D child in group.Children)
                    result = result * GetTransformMatrix(child);
                return result;
            }

            return Matrix3D.Identity;
        }

        private static void AddRenderables(BuildContext ctx, SceneNode node, FunctionEntity function)
        {
            Parameter resourceParam = function.GetParameter("resource");
            if (resourceParam?.content == null || resourceParam.content.dataType != DataType.RESOURCE)
                return;

            cResource resource = (cResource)resourceParam.content;
            ResourceReference renderable = resource.GetResource(ResourceType.RENDERABLE_INSTANCE);
            if (renderable == null)
                return;

            bool showTex = SettingsManager.GetBool(Singleton.Settings.ShowTexOpt);
            foreach (RenderableElements.Element element in renderable.RenderableInstance)
            {
                if (element?.Model == null)
                    continue;

                Materials.Material material = element.Material ?? element.Model.Material;
                GeometryModel3D geometry = Preview3DResourceCache.GetGeometry(element.Model, material, showTex);
                node.Content.Children.Add(geometry);
            }
        }

        public static bool GetEntityTransform(Entity entity, out Vector3 position, out Vector3 rotation)
        {
            position = Vector3.Zero;
            rotation = Vector3.Zero;
            if (entity == null)
                return false;

            Parameter positionParam = entity.GetParameter("position");
            if (positionParam?.content != null && positionParam.content.dataType == DataType.TRANSFORM)
            {
                cTransform transform = (cTransform)positionParam.content;
                position = transform.position;
                rotation = transform.rotation;
                return true;
            }
            return false;
        }

        public static void SetEntityTransform(Entity entity, Vector3 position, Vector3 rotation)
        {
            Parameter positionParam = entity.GetParameter("position");
            if (positionParam == null || positionParam.content?.dataType != DataType.TRANSFORM)
            {
                cTransform created = new cTransform(position, rotation);
                if (positionParam == null)
                    entity.AddParameter("position", created);
                else
                    positionParam.content = created;
                return;
            }

            cTransform transform = (cTransform)positionParam.content;
            transform.position = position;
            transform.rotation = rotation;
        }

        public static bool TryReadTransform(Transform3D transform, out Vector3 position, out Vector3 rotation)
        {
            position = Vector3.Zero;
            rotation = Vector3.Zero;
            if (transform == null)
                return false;

            return TryDecomposeMatrix(GetTransformMatrix(transform), out position, out rotation);
        }

        private static bool TryDecomposeMatrix(Matrix3D matrix, out Vector3 position, out Vector3 rotation)
        {
            position = Vector3.Zero;
            rotation = Vector3.Zero;

            Matrix4x4 numericsMatrix = new Matrix4x4(
                (float)matrix.M11, (float)matrix.M12, (float)matrix.M13, (float)matrix.M14,
                (float)matrix.M21, (float)matrix.M22, (float)matrix.M23, (float)matrix.M24,
                (float)matrix.M31, (float)matrix.M32, (float)matrix.M33, (float)matrix.M34,
                (float)matrix.OffsetX, (float)matrix.OffsetY, (float)matrix.OffsetZ, (float)matrix.M44);

            if (!Matrix4x4.Decompose(numericsMatrix, out Vector3 scale, out NumericsQuaternion quaternion, out Vector3 translation))
                return false;

            (decimal yaw, decimal pitch, decimal roll) = quaternion.ToYawPitchRoll();
            position = translation;
            rotation = new Vector3((float)pitch, (float)yaw, (float)roll);
            return true;
        }

        public static void SyncTransformRefsFromVisual(SceneNode node)
        {
            if (node?.Visual?.Transform == null)
                return;

            if (!TryReadTransform(node.Visual.Transform, out Vector3 position, out Vector3 rotation))
                return;

            SetNodeTransform(node, position, rotation);
        }

        /// <summary>Node whose visual receives transform edits (alias position applies to pointed target).</summary>
        public static SceneNode GetGizmoTarget(SceneNode node)
        {
            if (node?.PointedTarget != null && node.Entity.GetParameter("position") != null)
                return node.PointedTarget;
            return node;
        }

        /// <summary>Entity that owns the position parameter written on commit.</summary>
        public static SceneNode GetTransformOwner(SceneNode node)
        {
            if (node?.IsPointedTarget == true && node.OverrideOwner != null)
                return node.OverrideOwner;
            return node;
        }

        public static void ApplyNodeTransformToEntity(SceneNode node, bool syncVisual = true)
        {
            SceneNode owner = GetTransformOwner(node);
            SceneNode visualNode = GetGizmoTarget(node);

            if (!TryReadTransform(visualNode.Visual.Transform, out Vector3 position, out Vector3 rotation))
                return;

            SetEntityTransform(owner.Entity, position, rotation);

            if (!syncVisual)
            {
                if (owner.PointedTarget != null && owner.Entity.GetParameter("position") != null)
                    ApplyTransformToNode(owner.PointedTarget, position, rotation);
                return;
            }

            ApplyTransformToNode(owner, position, rotation);

            if (owner.PointedTarget != null && owner.Entity.GetParameter("position") != null)
                ApplyTransformToNode(owner.PointedTarget, position, rotation);
        }

        public static void SyncNodeFromEntity(SceneNode node)
        {
            SceneNode owner = GetTransformOwner(node);
            SceneNode visual = GetGizmoTarget(node);

            GetEntityTransform(owner.Entity, out Vector3 position, out Vector3 rotation);
            SetNodeTransform(visual, position, rotation);
        }
    }
}

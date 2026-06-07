using CATHODE.Scripting;
using CATHODE.Scripting.Internal;
using HelixToolkit.Wpf;
using OpenCAGE.DockPanels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Media3D;
using System.Windows.Threading;
using static OpenCAGE.CompositeSceneBuilder;

namespace OpenCAGE
{
    public partial class CompositeSceneViewer : UserControl
    {
        private SceneGraph _graph;
        private CompositeDisplay _compositeDisplay;
        private SceneNode _selectedNode;
        private EntityTransformGizmo _gizmo;
        private FpsCameraController _fpsCamera;
        private Visual3D _selectionOutline;
        private bool _suppressGizmoCallback;
        private bool _blockGizmoResync;

        public CompositeSceneViewer()
        {
            InitializeComponent();
            _gizmo = new EntityTransformGizmo(viewport);
            _fpsCamera = new FpsCameraController(viewport);
            _fpsCamera.MoveSpeedChanged += (_, __) => UpdateCameraSpeedLabel();
            _fpsCamera.Attach();
            UpdateCameraSpeedLabel();
            Unloaded += (_, __) => _fpsCamera?.Detach();

            localAxesCheckBox.Checked += LocalAxesCheckBox_Changed;
            localAxesCheckBox.Unchecked += LocalAxesCheckBox_Changed;

            ConfigureReferenceGrid();
            SetReferenceGridVisible(showGridCheckBox.IsChecked == true);

            Focusable = true;
            viewport.Focusable = true;
            PreviewKeyDown += CompositeSceneViewer_PreviewKeyDown;

            viewport.MouseDown += Viewport_MouseDown;
            viewport.MouseMove += Viewport_MouseMove;
            viewport.AddHandler(UIElement.PreviewMouseLeftButtonUpEvent, new MouseButtonEventHandler(Viewport_PreviewMouseLeftButtonUp), true);

            ApplyLocalAxesSetting();
            SetGizmoMode(GizmoInteractionMode.Translate, updateButtons: true);
        }

        private void UpdateCameraSpeedLabel()
        {
            if (cameraSpeedText == null || _fpsCamera == null)
                return;

            double speed = _fpsCamera.MoveSpeed;
            string formatted = speed >= 10 ? speed.ToString("0") : speed.ToString("0.0");
            cameraSpeedText.Text = $"Camera speed: {formatted}  (scroll to adjust)";
        }

        private void LocalAxesCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            ApplyLocalAxesSetting();
        }

        private void ApplyLocalAxesSetting()
        {
            if (_gizmo == null || localAxesCheckBox == null)
                return;

            _gizmo.SetTranslateInLocalSpace(localAxesCheckBox.IsChecked == true);
        }

        private void CompositeSceneViewer_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.IsRepeat)
                return;

            if (IsMovementKey(e.Key) && viewport.IsKeyboardFocusWithin)
                return;

            if (Keyboard.Modifiers != ModifierKeys.None)
                return;

            if (e.Key == Key.D1 || e.Key == Key.NumPad1)
            {
                SetGizmoMode(GizmoInteractionMode.Translate, updateButtons: true);
                e.Handled = true;
                return;
            }

            if (e.Key == Key.D2 || e.Key == Key.NumPad2)
            {
                SetGizmoMode(GizmoInteractionMode.Rotate, updateButtons: true);
                e.Handled = true;
                return;
            }

            if (e.Key != Key.Z)
                return;

            if (_selectedNode == null)
                return;

            FocusSelectedEntity();
            e.Handled = true;
        }

        public void ShowScene(SceneGraph graph, CompositeDisplay compositeDisplay)
        {
            _graph = graph;
            _compositeDisplay = compositeDisplay;

            DetachGizmo();
            ClearSelectionOutline();
            _selectedNode = null;

            sceneRoot.Children.Clear();
            transparentSorter.Children.Clear();

            if (graph?.Root == null)
                return;

            SortTransparentMeshes(graph);
            sceneRoot.Children.Add(graph.Root);

            Rect3D? bounds = TryGetSceneBounds();
            UpdateReferenceGrid(bounds);
            viewport.Dispatcher.BeginInvoke(new Action(() => FrameSceneContent()), DispatcherPriority.Loaded);

            Entity selected = compositeDisplay?.EntityDisplay?.Entity;
            if (selected != null)
                SelectEntity(selected.shortGUID, focusCamera: false);
        }

        public void SelectEntity(ShortGuid entityId, bool focusCamera = true)
        {
            if (_graph == null || entityId == ShortGuid.Invalid)
            {
                ClearSelection();
                return;
            }

            if (!_graph.ByEntity.TryGetValue(entityId, out SceneNode node))
            {
                ClearSelection();
                return;
            }

            if (node.IsPointedTarget && node.OverrideOwner != null)
                node = node.OverrideOwner;

            SelectNode(node, focusCamera);
        }

        public void SyncSelectedFromEntity()
        {
            if (_selectedNode == null || _blockGizmoResync || _gizmo.IsDragging)
                return;

            _suppressGizmoCallback = true;
            try
            {
                SyncNodeFromEntity(_selectedNode);
                _graph?.RefreshTransparentTransforms();
                if (_selectedNode != null)
                {
                    AttachGizmo(_selectedNode);
                    SyncSelectionOutlineTransform();
                }
            }
            finally
            {
                _suppressGizmoCallback = false;
            }
        }

        private void MoveModeButton_Click(object sender, RoutedEventArgs e)
        {
            SetGizmoMode(GizmoInteractionMode.Translate, updateButtons: true);
        }

        private void RotateModeButton_Click(object sender, RoutedEventArgs e)
        {
            SetGizmoMode(GizmoInteractionMode.Rotate, updateButtons: true);
        }

        private void ShowGridCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            SetReferenceGridVisible(showGridCheckBox.IsChecked == true);
        }

        private void ConfigureReferenceGrid()
        {
            referenceGrid.Normal = new Vector3D(0, 1, 0);
            referenceGrid.LengthDirection = new Vector3D(1, 0, 0);
        }

        private void SetReferenceGridVisible(bool visible)
        {
            if (visible)
            {
                if (!viewport.Children.Contains(referenceGrid))
                    viewport.Children.Insert(1, referenceGrid);
            }
            else if (viewport.Children.Contains(referenceGrid))
            {
                viewport.Children.Remove(referenceGrid);
            }
        }

        private void FrameSceneContent(double animationTime = 0)
        {
            viewport.ModelUpDirection = new Vector3D(0, 1, 0);
            viewport.Camera.NearPlaneDistance = 0.01;
            viewport.Camera.UpDirection = new Vector3D(0, 1, 0);
            viewport.Camera.LookDirection = new Vector3D(-0.5, -0.5, -1.0);

            Rect3D? bounds = TryGetSceneBounds();
            if (bounds.HasValue && !bounds.Value.IsEmpty)
                viewport.ZoomExtents(InflateBounds(bounds.Value, 1.15), animationTime);
            else
                viewport.ZoomExtents(animationTime);
        }

        private void UpdateReferenceGrid(Rect3D? bounds)
        {
            if (showGridCheckBox.IsChecked != true)
                return;

            Point3D center = new Point3D(0, 0, 0);
            double size = 8;

            if (bounds.HasValue && !bounds.Value.IsEmpty)
            {
                Rect3D box = bounds.Value;
                center = new Point3D(
                    box.X + box.SizeX * 0.5,
                    box.Y,
                    box.Z + box.SizeZ * 0.5);
                size = Math.Max(Math.Max(box.SizeX, box.SizeY), box.SizeZ);
                size = Math.Max(size * 2.0, 8);
            }

            referenceGrid.Center = center;
            referenceGrid.Width = size;
            referenceGrid.Length = size;
        }

        private static Rect3D TransformBounds(Rect3D bounds, Matrix3D matrix)
        {
            if (bounds.IsEmpty)
                return bounds;

            Point3D[] corners = new Point3D[]
            {
                new Point3D(bounds.X, bounds.Y, bounds.Z),
                new Point3D(bounds.X + bounds.SizeX, bounds.Y, bounds.Z),
                new Point3D(bounds.X, bounds.Y + bounds.SizeY, bounds.Z),
                new Point3D(bounds.X + bounds.SizeX, bounds.Y + bounds.SizeY, bounds.Z),
                new Point3D(bounds.X, bounds.Y, bounds.Z + bounds.SizeZ),
                new Point3D(bounds.X + bounds.SizeX, bounds.Y, bounds.Z + bounds.SizeZ),
                new Point3D(bounds.X, bounds.Y + bounds.SizeY, bounds.Z + bounds.SizeZ),
                new Point3D(bounds.X + bounds.SizeX, bounds.Y + bounds.SizeY, bounds.Z + bounds.SizeZ),
            };

            Point3D first = matrix.Transform(corners[0]);
            double minX = first.X, maxX = first.X;
            double minY = first.Y, maxY = first.Y;
            double minZ = first.Z, maxZ = first.Z;

            for (int i = 1; i < corners.Length; i++)
            {
                Point3D p = matrix.Transform(corners[i]);
                if (p.X < minX) minX = p.X;
                if (p.X > maxX) maxX = p.X;
                if (p.Y < minY) minY = p.Y;
                if (p.Y > maxY) maxY = p.Y;
                if (p.Z < minZ) minZ = p.Z;
                if (p.Z > maxZ) maxZ = p.Z;
            }

            return new Rect3D(minX, minY, minZ, maxX - minX, maxY - minY, maxZ - minZ);
        }

        private static Rect3D InflateBounds(Rect3D bounds, double factor)
        {
            if (bounds.IsEmpty || factor <= 1)
                return bounds;

            double inflateX = bounds.SizeX * (factor - 1) * 0.5;
            double inflateY = bounds.SizeY * (factor - 1) * 0.5;
            double inflateZ = bounds.SizeZ * (factor - 1) * 0.5;
            return new Rect3D(
                bounds.X - inflateX,
                bounds.Y - inflateY,
                bounds.Z - inflateZ,
                bounds.SizeX + inflateX * 2,
                bounds.SizeY + inflateY * 2,
                bounds.SizeZ + inflateZ * 2);
        }

        private Rect3D? TryGetSceneBounds()
        {
            if (_graph == null)
                return null;

            Rect3D? bounds = null;
            foreach (SceneNode node in _graph.Nodes)
            {
                if (node.Content.Children.Count == 0)
                    continue;

                Rect3D localBounds = node.Content.Bounds;
                if (localBounds.IsEmpty)
                    continue;

                Matrix3D matrix = GetWorldMatrix3D(node);

                Rect3D worldBounds = TransformBounds(localBounds, matrix);
                bounds = bounds.HasValue ? Rect3D.Union(bounds.Value, worldBounds) : worldBounds;
            }

            return bounds;
        }

        private Rect3D? GetNodeBounds(SceneNode node)
        {
            SceneNode boundsNode = GetGizmoTarget(node) ?? node;
            if (boundsNode?.Content == null || boundsNode.Content.Children.Count == 0)
                return null;

            Rect3D localBounds = boundsNode.Content.Bounds;
            if (localBounds.IsEmpty)
                return null;

            return TransformBounds(localBounds, GetWorldMatrix3D(boundsNode));
        }

        private void SetGizmoMode(GizmoInteractionMode mode, bool updateButtons)
        {
            if (_gizmo.IsDragging)
                CommitTransform();

            _gizmo.SetMode(mode);
            ApplyLocalAxesSetting();

            if (updateButtons)
            {
                moveModeButton.IsChecked = mode == GizmoInteractionMode.Translate;
                rotateModeButton.IsChecked = mode == GizmoInteractionMode.Rotate;
            }
        }

        private void SelectNode(SceneNode node, bool focusCamera)
        {
            _selectedNode = node;
            AttachGizmo(node);
            UpdateSelectionOutline();

            if (focusCamera)
                FocusSelectedEntity();
        }

        private void FocusSelectedEntity()
        {
            if (_selectedNode == null)
                return;

            Rect3D? bounds = GetNodeBounds(_selectedNode);
            if (bounds.HasValue && !bounds.Value.IsEmpty)
                viewport.ZoomExtents(InflateBounds(bounds.Value, 1.35), 400);
            else
                FrameSceneContent(400);
        }

        private void ClearSelection()
        {
            _selectedNode = null;
            DetachGizmo();
            ClearSelectionOutline();
        }

        private void UpdateSelectionOutline()
        {
            ClearSelectionOutline();
            if (_selectedNode == null)
                return;

            SceneNode outlineNode = GetGizmoTarget(_selectedNode) ?? _selectedNode;
            _selectionOutline = SelectionOutlineBuilder.Create(outlineNode);
            if (_selectionOutline == null)
                return;

            if (!viewport.Children.Contains(_selectionOutline))
                viewport.Children.Add(_selectionOutline);
        }

        private void SyncSelectionOutlineTransform()
        {
            if (_selectionOutline == null || _selectedNode == null)
                return;

            SceneNode outlineNode = GetGizmoTarget(_selectedNode) ?? _selectedNode;
            _selectionOutline.Transform = new MatrixTransform3D(GetWorldMatrix3D(outlineNode));
        }

        private void ClearSelectionOutline()
        {
            if (_selectionOutline != null && viewport.Children.Contains(_selectionOutline))
                viewport.Children.Remove(_selectionOutline);

            _selectionOutline = null;
        }

        private void AttachGizmo(SceneNode node)
        {
            DetachGizmo();
            if (node?.Visual == null)
                return;

            _gizmo.Attach(node);
            if (!viewport.Children.Contains(_gizmo.Root))
                viewport.Children.Add(_gizmo.Root);
        }

        private void DetachGizmo()
        {
            _gizmo.Detach();
            if (viewport.Children.Contains(_gizmo.Root))
                viewport.Children.Remove(_gizmo.Root);
        }

        private void Viewport_MouseMove(object sender, MouseEventArgs e)
        {
            if (_suppressGizmoCallback || !_gizmo.IsDragging)
                return;

            if (e.LeftButton != MouseButtonState.Pressed)
            {
                CommitTransform();
                return;
            }

            _gizmo.Drag(e.GetPosition(viewport));
            _graph?.RefreshTransparentTransforms();
            SyncSelectionOutlineTransform();
        }

        private void Viewport_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (_gizmo.IsDragging)
                CommitTransform();
        }

        private void CommitTransform()
        {
            if (_suppressGizmoCallback || _blockGizmoResync || _selectedNode == null)
                return;

            _gizmo.EndDrag();

            _blockGizmoResync = true;
            try
            {
                SceneNode source = GetTransformOwner(_selectedNode);

                bool hadPosition = source.Entity.GetParameter("position") != null;

                ApplyNodeTransformToEntity(_selectedNode, syncVisual: false);

                cTransform transform = (cTransform)source.Entity.GetParameter("position")?.content;
                if (transform == null)
                {
                    GetEntityTransform(source.Entity, out System.Numerics.Vector3 pos, out System.Numerics.Vector3 rot);
                    transform = new cTransform(pos, rot);
                }

                Singleton.OnEntityMoved?.Invoke(transform, source.Entity);

                bool positionWasAdded = !hadPosition && source.Entity.GetParameter("position") != null;
                if (positionWasAdded)
                    _compositeDisplay?.ReloadEntity(source.Entity);
                else
                    _compositeDisplay?.EntityDisplay?.ApplyTransformFromExternal(transform);

                Singleton.OnParameterModified?.Invoke();

                _gizmo.UpdateGizmoTransform();
                _graph?.RefreshTransparentTransforms();
                SyncSelectionOutlineTransform();
            }
            finally
            {
                _blockGizmoResync = false;
            }
        }

        private static bool IsMovementKey(Key key)
        {
            return key == Key.W || key == Key.A || key == Key.S || key == Key.D || key == Key.Space || key == Key.C;
        }

        private void Viewport_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Right)
                return;

            if (_graph == null || e.ChangedButton != MouseButton.Left)
                return;

            viewport.Focus();
            Point hitPoint = e.GetPosition(viewport);

            if (_selectedNode != null && _gizmo.TryStartDrag(hitPoint))
            {
                e.Handled = true;
                return;
            }

            IList<Viewport3DHelper.HitResult> hits = Viewport3DHelper.FindHits(viewport.Viewport, hitPoint);
            foreach (Viewport3DHelper.HitResult hit in hits)
            {
                SceneNode node = FindNodeForModel(hit.Model);
                if (node == null)
                    continue;

                if (node.IsPointedTarget && node.OverrideOwner != null)
                    node = node.OverrideOwner;

                SelectNode(node, focusCamera: false);
                _compositeDisplay?.LoadEntity(node.Entity, false);
                e.Handled = true;
                return;
            }
        }

        private SceneNode FindNodeForModel(Model3D model)
        {
            if (model == null || _graph == null)
                return null;

            foreach (SceneNode node in _graph.Nodes)
            {
                if (ContainsModel(node.Content, model))
                    return node;
            }
            return null;
        }

        private static bool ContainsModel(Model3DGroup group, Model3D model)
        {
            if (group == null)
                return false;

            foreach (Model3D child in group.Children)
            {
                if (child == model)
                    return true;
                if (child is Model3DGroup childGroup && ContainsModel(childGroup, model))
                    return true;
            }
            return false;
        }

        private void SortTransparentMeshes(SceneGraph graph)
        {
            foreach (SceneNode node in graph.Nodes)
            {
                List<GeometryModel3D> opaque = new List<GeometryModel3D>();
                List<GeometryModel3D> transparent = new List<GeometryModel3D>();

                foreach (Model3D child in node.Content.Children.ToList())
                {
                    if (child is GeometryModel3D geometry)
                    {
                        if (AlienPAK.MaterialApplier.GetIsTransparent(geometry))
                            transparent.Add(geometry);
                        else
                            opaque.Add(geometry);
                    }
                }

                node.Content.Children.Clear();
                foreach (GeometryModel3D geometry in opaque)
                    node.Content.Children.Add(geometry);

                if (transparent.Count == 0)
                    continue;

                Model3DGroup transparentGroup = new Model3DGroup();
                foreach (GeometryModel3D geometry in transparent)
                    transparentGroup.Children.Add(geometry);

                ModelVisual3D transparentVisual = new ModelVisual3D
                {
                    Transform = new MatrixTransform3D(GetWorldMatrix3D(node)),
                    Content = transparentGroup,
                };
                node.TransparentVisual = transparentVisual;
                transparentSorter.Children.Add(transparentVisual);
            }
        }
    }
}

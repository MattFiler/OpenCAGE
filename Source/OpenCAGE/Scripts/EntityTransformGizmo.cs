using HelixToolkit.Wpf;
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using NumericsQuaternion = System.Numerics.Quaternion;
using static OpenCAGE.CompositeSceneBuilder;

namespace OpenCAGE
{
    public enum GizmoInteractionMode
    {
        Translate,
        Rotate,
    }

    /// <summary>
    /// Screen-space transform gizmo that edits SceneNode transforms directly,
    /// avoiding Helix manipulators that require Viewport3D ancestry.
    /// </summary>
    public class EntityTransformGizmo
    {
        private enum DragMode
        {
            None,
            TranslateX,
            TranslateY,
            TranslateZ,
            RotateX,
            RotateY,
            RotateZ,
        }

        private readonly HelixViewport3D _viewport;
        private readonly ModelVisual3D _root = new ModelVisual3D();
        private readonly ArrowVisual3D _axisX = CreateAxis(Colors.Red, new Vector3D(1, 0, 0));
        private readonly ArrowVisual3D _axisY = CreateAxis(Colors.Green, new Vector3D(0, 1, 0));
        private readonly ArrowVisual3D _axisZ = CreateAxis(Colors.Blue, new Vector3D(0, 0, 1));
        private readonly ModelVisual3D _ringX;
        private readonly ModelVisual3D _ringY;
        private readonly ModelVisual3D _ringZ;
        private readonly TranslateTransform3D _ringXCenter = new TranslateTransform3D();
        private readonly TranslateTransform3D _ringYCenter = new TranslateTransform3D();
        private readonly TranslateTransform3D _ringZCenter = new TranslateTransform3D();

        private SceneNode _node;
        private SceneNode _editNode;
        private GizmoInteractionMode _mode = GizmoInteractionMode.Translate;
        private bool _translateInLocalSpace = true;
        private DragMode _dragMode = DragMode.None;
        private Point _dragStartScreen;
        private Matrix4x4 _dragStartWorld = Matrix4x4.Identity;
        private Point3D _dragPivotWorld;
        private Vector3D _dragAxisWorld;
        private Vector3D _dragPlaneNormal;

        private const double AxisScreenPickPixels = 28.0;
        private const double RingScreenPickPixels = 32.0;
        private const int RingHitTestSamples = 40;
        private const double AxisLength = 1.5;
        private const double RingRadius = 1.35;

        public EntityTransformGizmo(HelixViewport3D viewport)
        {
            _viewport = viewport;

            _ringX = CreateRingVisual(Colors.Red, _ringXCenter, new RotateTransform3D(new AxisAngleRotation3D(new Vector3D(0, 1, 0), 90)));
            _ringY = CreateRingVisual(Colors.Green, _ringYCenter, new RotateTransform3D(new AxisAngleRotation3D(new Vector3D(1, 0, 0), -90)));
            _ringZ = CreateRingVisual(Colors.Blue, _ringZCenter, Transform3D.Identity);

            ApplyModeVisibility();
        }

        public ModelVisual3D Root => _root;

        public bool IsDragging => _dragMode != DragMode.None;

        public GizmoInteractionMode Mode => _mode;

        public bool TranslateInLocalSpace => _translateInLocalSpace;

        public void SetTranslateInLocalSpace(bool localSpace)
        {
            _translateInLocalSpace = localSpace;
            if (_node != null)
                UpdateGizmoTransform();
        }

        public void SetMode(GizmoInteractionMode mode)
        {
            _mode = mode;
            EndDrag();
            ApplyModeVisibility();
        }

        public void Attach(SceneNode node)
        {
            _node = node;
            ApplyModeVisibility();
            UpdateGizmoTransform();
        }

        public void Detach()
        {
            _node = null;
            _dragMode = DragMode.None;
            _root.Transform = null;
        }

        public void UpdateGizmoTransform()
        {
            _editNode = GetGizmoTarget(_node);
            if (_editNode?.Transform == null)
                return;

            _root.Transform = new MatrixTransform3D(GetWorldMatrix3D(_editNode));

            Point3D center = GetLocalCenter(_editNode);
            _axisX.Point1 = center;
            _axisX.Point2 = center + new Vector3D(AxisLength, 0, 0);
            _axisY.Point1 = center;
            _axisY.Point2 = center + new Vector3D(0, AxisLength, 0);
            _axisZ.Point1 = center;
            _axisZ.Point2 = center + new Vector3D(0, 0, AxisLength);

            _ringXCenter.OffsetX = center.X;
            _ringXCenter.OffsetY = center.Y;
            _ringXCenter.OffsetZ = center.Z;
            _ringYCenter.OffsetX = center.X;
            _ringYCenter.OffsetY = center.Y;
            _ringYCenter.OffsetZ = center.Z;
            _ringZCenter.OffsetX = center.X;
            _ringZCenter.OffsetY = center.Y;
            _ringZCenter.OffsetZ = center.Z;
        }

        public bool TryStartDrag(Point screenPoint)
        {
            if (_node == null)
                return false;

            DragMode mode = HitTestVisual(screenPoint);
            if (mode == DragMode.None)
                mode = HitTest(screenPoint);
            if (mode == DragMode.None)
                return false;

            _editNode = GetGizmoTarget(_node);
            _dragStartWorld = GetWorldMatrix(_editNode);

            _dragMode = mode;
            _dragStartScreen = screenPoint;
            _dragPivotWorld = GetWorldPoint(GetLocalCenter(_editNode));
            _dragAxisWorld = GetWorldAxis(mode);

            if (IsRotateMode(mode))
                _dragPlaneNormal = _dragAxisWorld;
            else
                _dragPlaneNormal = GetCameraLookDirection();

            return true;
        }

        public bool Drag(Point screenPoint)
        {
            if (_dragMode == DragMode.None || _node == null)
                return false;

            if (IsRotateMode(_dragMode))
                ApplyRotationDrag(screenPoint);
            else
                ApplyTranslationDrag(screenPoint);

            UpdateGizmoTransform();
            return true;
        }

        public void EndDrag()
        {
            _dragMode = DragMode.None;
        }

        private void ApplyTranslationDrag(Point screenPoint)
        {
            Point3D? startHit = UnProject(_dragStartScreen, _dragPivotWorld, _dragPlaneNormal);
            Point3D? currentHit = UnProject(screenPoint, _dragPivotWorld, _dragPlaneNormal);
            if (startHit == null || currentHit == null)
                return;

            Vector3D worldDelta = currentHit.Value - startHit.Value;
            double delta = Vector3D.DotProduct(worldDelta, _dragAxisWorld);

            if (!Matrix4x4.Decompose(_dragStartWorld, out _, out NumericsQuaternion rotation, out Vector3 translation))
                return;

            Vector3 offset;
            if (_translateInLocalSpace)
            {
                Vector3D axis = _dragAxisWorld;
                axis.Normalize();
                offset = new Vector3((float)(axis.X * delta), (float)(axis.Y * delta), (float)(axis.Z * delta));
            }
            else
            {
                Vector3 worldAxis = GetLocalAxis(_dragMode);
                offset = worldAxis * (float)delta;
            }

            Matrix4x4 newWorld = Matrix4x4.CreateFromQuaternion(rotation) * Matrix4x4.CreateTranslation(translation + offset);
            SetNodeTransformFromWorld(_editNode, newWorld);
        }

        private void ApplyRotationDrag(Point screenPoint)
        {
            Vector3D axisWorld = _dragAxisWorld;
            axisWorld.Normalize();

            Point3D? startHit = UnProject(_dragStartScreen, _dragPivotWorld, axisWorld);
            Point3D? currentHit = UnProject(screenPoint, _dragPivotWorld, axisWorld);
            if (startHit == null || currentHit == null)
                return;

            Vector3D from = startHit.Value - _dragPivotWorld;
            Vector3D to = currentHit.Value - _dragPivotWorld;
            if (from.LengthSquared < 1e-8 || to.LengthSquared < 1e-8)
                return;

            from.Normalize();
            to.Normalize();

            Vector3D cross = Vector3D.CrossProduct(from, to);
            double sign = Math.Sign(Vector3D.DotProduct(cross, axisWorld));
            double dot = Math.Max(-1.0, Math.Min(1.0, Vector3D.DotProduct(from, to)));
            double angleRad = sign * Math.Acos(dot);

            Vector3 pivot = new Vector3((float)_dragPivotWorld.X, (float)_dragPivotWorld.Y, (float)_dragPivotWorld.Z);
            Vector3 axis = new Vector3((float)axisWorld.X, (float)axisWorld.Y, (float)axisWorld.Z);
            NumericsQuaternion deltaRotation = NumericsQuaternion.CreateFromAxisAngle(axis, (float)angleRad);

            Matrix4x4 toPivot = Matrix4x4.CreateTranslation(pivot);
            Matrix4x4 fromPivot = Matrix4x4.CreateTranslation(-pivot);
            Matrix4x4 newWorld = toPivot * Matrix4x4.CreateFromQuaternion(deltaRotation) * fromPivot * _dragStartWorld;
            SetNodeTransformFromWorld(_editNode, newWorld);
        }

        private void ApplyModeVisibility()
        {
            _root.Children.Clear();

            if (_mode == GizmoInteractionMode.Translate)
            {
                _root.Children.Add(_axisX);
                _root.Children.Add(_axisY);
                _root.Children.Add(_axisZ);
            }
            else
            {
                _root.Children.Add(_ringX);
                _root.Children.Add(_ringY);
                _root.Children.Add(_ringZ);
            }
        }

        private DragMode HitTestVisual(Point screenPoint)
        {
            IList<Viewport3DHelper.HitResult> hits = Viewport3DHelper.FindHits(_viewport.Viewport, screenPoint);
            foreach (Viewport3DHelper.HitResult hit in hits)
            {
                if (hit.Visual == null)
                    continue;

                if (_mode == GizmoInteractionMode.Translate)
                {
                    if (IsVisualOrDescendant(hit.Visual, _axisX)) return DragMode.TranslateX;
                    if (IsVisualOrDescendant(hit.Visual, _axisY)) return DragMode.TranslateY;
                    if (IsVisualOrDescendant(hit.Visual, _axisZ)) return DragMode.TranslateZ;
                }
                else
                {
                    if (IsVisualOrDescendant(hit.Visual, _ringX)) return DragMode.RotateX;
                    if (IsVisualOrDescendant(hit.Visual, _ringY)) return DragMode.RotateY;
                    if (IsVisualOrDescendant(hit.Visual, _ringZ)) return DragMode.RotateZ;
                }
            }

            return DragMode.None;
        }

        private static bool IsVisualOrDescendant(DependencyObject visual, Visual3D ancestor)
        {
            DependencyObject current = visual;
            while (current != null)
            {
                if (current == ancestor)
                    return true;
                current = VisualTreeHelper.GetParent(current);
            }
            return false;
        }

        private DragMode HitTest(Point screenPoint)
        {
            Point3D centerWorld = GetWorldPoint(GetLocalCenter(GetGizmoTarget(_node)));
            Point centerScreen = WorldToScreen(centerWorld);

            DragMode bestMode = DragMode.None;
            double bestDistance = double.MaxValue;

            if (_mode == GizmoInteractionMode.Rotate)
            {
                TestRotateRing(screenPoint, centerScreen, centerWorld, new Vector3D(1, 0, 0), DragMode.RotateX, ref bestMode, ref bestDistance);
                TestRotateRing(screenPoint, centerScreen, centerWorld, new Vector3D(0, 1, 0), DragMode.RotateY, ref bestMode, ref bestDistance);
                TestRotateRing(screenPoint, centerScreen, centerWorld, new Vector3D(0, 0, 1), DragMode.RotateZ, ref bestMode, ref bestDistance);
            }
            else
            {
                Vector3D axisX = _translateInLocalSpace ? TransformLocalDirection(new Vector3D(1, 0, 0)) : new Vector3D(1, 0, 0);
                Vector3D axisY = _translateInLocalSpace ? TransformLocalDirection(new Vector3D(0, 1, 0)) : new Vector3D(0, 1, 0);
                Vector3D axisZ = _translateInLocalSpace ? TransformLocalDirection(new Vector3D(0, 0, 1)) : new Vector3D(0, 0, 1);
                TestTranslateAxis(screenPoint, centerScreen, centerWorld, axisX, DragMode.TranslateX, ref bestMode, ref bestDistance);
                TestTranslateAxis(screenPoint, centerScreen, centerWorld, axisY, DragMode.TranslateY, ref bestMode, ref bestDistance);
                TestTranslateAxis(screenPoint, centerScreen, centerWorld, axisZ, DragMode.TranslateZ, ref bestMode, ref bestDistance);
            }

            return bestMode;
        }

        private void TestRotateRing(
            Point screenPoint,
            Point centerScreen,
            Point3D centerWorld,
            Vector3D localAxis,
            DragMode rotateMode,
            ref DragMode bestMode,
            ref double bestDistance)
        {
            GetRingBasis(localAxis, out Vector3D basisU, out Vector3D basisV);

            Point previousScreen = centerScreen;
            double minDistance = double.MaxValue;

            for (int i = 0; i <= RingHitTestSamples; i++)
            {
                double angle = (Math.PI * 2.0 * i) / RingHitTestSamples;
                Vector3D localOffset = (basisU * Math.Cos(angle)) + (basisV * Math.Sin(angle));
                Vector3D worldOffset = TransformLocalDirection(localOffset);
                Point3D worldPoint = centerWorld + (worldOffset * RingRadius);
                Point screenPointOnRing = WorldToScreen(worldPoint);

                if (i > 0)
                    minDistance = Math.Min(minDistance, DistancePointToSegment(screenPoint, previousScreen, screenPointOnRing));

                previousScreen = screenPointOnRing;
            }

            if (minDistance < RingScreenPickPixels && minDistance < bestDistance)
            {
                bestDistance = minDistance;
                bestMode = rotateMode;
            }
        }

        private void TestTranslateAxis(
            Point screenPoint,
            Point centerScreen,
            Point3D centerWorld,
            Vector3D worldAxis,
            DragMode translateMode,
            ref DragMode bestMode,
            ref double bestDistance)
        {
            Point3D endWorld = centerWorld + worldAxis * AxisLength;
            Point endScreen = WorldToScreen(endWorld);

            double distance = DistancePointToSegment(screenPoint, centerScreen, endScreen);
            if (distance < AxisScreenPickPixels && distance < bestDistance)
            {
                bestDistance = distance;
                bestMode = translateMode;
            }
        }

        private static void GetRingBasis(Vector3D axis, out Vector3D u, out Vector3D v)
        {
            axis.Normalize();
            Vector3D fallback = Math.Abs(Vector3D.DotProduct(axis, new Vector3D(0, 1, 0))) > 0.9
                ? new Vector3D(1, 0, 0)
                : new Vector3D(0, 1, 0);
            u = Vector3D.CrossProduct(axis, fallback);
            u.Normalize();
            v = Vector3D.CrossProduct(axis, u);
            v.Normalize();
        }

        private Vector3D TransformLocalDirection(Vector3D localAxis)
        {
            SceneNode edit = GetGizmoTarget(_node);
            if (edit == null)
                return localAxis;

            Matrix3D matrix = GetWorldMatrix3D(edit);
            matrix.OffsetX = 0;
            matrix.OffsetY = 0;
            matrix.OffsetZ = 0;
            return matrix.Transform(localAxis);
        }

        private Vector3D GetWorldAxis(DragMode mode)
        {
            if (_mode == GizmoInteractionMode.Translate && !_translateInLocalSpace)
                return GetWorldUnitAxis(mode);

            switch (mode)
            {
                case DragMode.TranslateX:
                case DragMode.RotateX:
                    return TransformLocalDirection(new Vector3D(1, 0, 0));
                case DragMode.TranslateY:
                case DragMode.RotateY:
                    return TransformLocalDirection(new Vector3D(0, 1, 0));
                default:
                    return TransformLocalDirection(new Vector3D(0, 0, 1));
            }
        }

        private static Vector3D GetWorldUnitAxis(DragMode mode)
        {
            switch (mode)
            {
                case DragMode.TranslateX:
                case DragMode.RotateX:
                    return new Vector3D(1, 0, 0);
                case DragMode.TranslateY:
                case DragMode.RotateY:
                    return new Vector3D(0, 1, 0);
                default:
                    return new Vector3D(0, 0, 1);
            }
        }

        private static Vector3 GetLocalAxis(DragMode mode)
        {
            switch (mode)
            {
                case DragMode.TranslateX: return Vector3.UnitX;
                case DragMode.TranslateY: return Vector3.UnitY;
                default: return Vector3.UnitZ;
            }
        }

        private static bool IsRotateMode(DragMode mode)
        {
            return mode == DragMode.RotateX || mode == DragMode.RotateY || mode == DragMode.RotateZ;
        }

        private Point3D GetWorldPoint(Point3D localPoint)
        {
            SceneNode edit = GetGizmoTarget(_node);
            if (edit == null)
                return localPoint;

            return GetWorldMatrix3D(edit).Transform(localPoint);
        }

        private Point WorldToScreen(Point3D world)
        {
            return Viewport3DHelper.Point3DtoPoint2D(_viewport.Viewport, world);
        }

        private Point3D? UnProject(Point screen, Point3D planePoint, Vector3D planeNormal)
        {
            return _viewport.Viewport.UnProject(screen, planePoint, planeNormal);
        }

        private Vector3D GetCameraLookDirection()
        {
            ProjectionCamera camera = _viewport.Camera as ProjectionCamera;
            if (camera == null)
                return new Vector3D(0, 0, -1);

            return camera.LookDirection;
        }

        private static double DistancePointToSegment(Point p, Point a, Point b)
        {
            System.Windows.Vector ap = p - a;
            System.Windows.Vector ab = b - a;
            double abLenSq = ab.LengthSquared;
            if (abLenSq < 1e-6)
                return ap.Length;

            double t = Math.Max(0, Math.Min(1, System.Windows.Vector.Multiply(ap, ab) / abLenSq));
            Point closest = a + ab * t;
            return (p - closest).Length;
        }

        private static ArrowVisual3D CreateAxis(Color color, Vector3D direction)
        {
            Point3D origin = new Point3D(0, 0, 0);
            return new ArrowVisual3D
            {
                Diameter = 0.1,
                Fill = new SolidColorBrush(color),
                Point1 = origin,
                Point2 = origin + direction * AxisLength,
            };
        }

        private static ModelVisual3D CreateRingVisual(Color color, TranslateTransform3D center, Transform3D orientation)
        {
            TorusVisual3D torus = new TorusVisual3D
            {
                TorusDiameter = RingRadius * 2.0,
                TubeDiameter = 0.12,
                ThetaDiv = 48,
                PhiDiv = 24,
                Fill = new SolidColorBrush(color) { Opacity = 0.55 },
            };

            Transform3DGroup transform = new Transform3DGroup();
            transform.Children.Add(center);
            if (orientation != null && orientation != Transform3D.Identity)
                transform.Children.Add(orientation);

            ModelVisual3D host = new ModelVisual3D { Transform = transform };
            host.Children.Add(torus);
            return host;
        }

        private static Point3D GetLocalCenter(SceneNode node)
        {
            Rect3D bounds = node.Content.Bounds;
            if (bounds.IsEmpty)
                return new Point3D(0, 0, 0);

            return new Point3D(
                bounds.X + bounds.SizeX * 0.5,
                bounds.Y + bounds.SizeY * 0.5,
                bounds.Z + bounds.SizeZ * 0.5);
        }
    }
}

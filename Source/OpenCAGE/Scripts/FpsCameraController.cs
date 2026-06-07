using HelixToolkit.Wpf;
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Media3D;

namespace OpenCAGE
{
    /// <summary>
    /// WASD fly camera with right-mouse look for Helix viewport previews.
    /// </summary>
    public sealed class FpsCameraController
    {
        private readonly HelixViewport3D _viewport;
        private readonly HashSet<Key> _keysHeld = new HashSet<Key>();
        private bool _isLooking;
        private Point _lastMouse;
        private bool _attached;
        private DateTime _lastMoveTime = DateTime.UtcNow;
        private double _moveSpeed = 12.0;

        public double MinMoveSpeed { get; set; } = 0.5;
        public double MaxMoveSpeed { get; set; } = 500.0;
        public double ScrollSpeedFactor { get; set; } = 1.12;

        public double MoveSpeed
        {
            get => _moveSpeed;
            set => SetMoveSpeed(value);
        }

        public double SprintMultiplier = 2.5;
        public double LookSensitivity = 0.35;
        public double MinPitchDegrees = -89.0;
        public double MaxPitchDegrees = 89.0;

        public event EventHandler MoveSpeedChanged;

        public FpsCameraController(HelixViewport3D viewport)
        {
            _viewport = viewport ?? throw new ArgumentNullException(nameof(viewport));
        }

        public void Attach()
        {
            if (_attached)
                return;

            _attached = true;
            _viewport.IsRotationEnabled = false;
            _viewport.IsZoomEnabled = false;
            _viewport.IsMoveEnabled = false;
            _viewport.IsPanEnabled = false;
            _viewport.PreviewKeyDown += Viewport_PreviewKeyDown;
            _viewport.PreviewKeyUp += Viewport_PreviewKeyUp;
            _viewport.KeyDown += Viewport_BlockHelixKeyDown;
            _viewport.PreviewMouseRightButtonDown += Viewport_PreviewMouseRightButtonDown;
            _viewport.PreviewMouseRightButtonUp += Viewport_PreviewMouseRightButtonUp;
            _viewport.PreviewMouseMove += Viewport_PreviewMouseMove;
            _viewport.PreviewMouseWheel += Viewport_PreviewMouseWheel;
            _viewport.LostKeyboardFocus += Viewport_LostKeyboardFocus;
            CompositionTarget.Rendering += CompositionTarget_Rendering;
        }

        public void SetMoveSpeed(double speed)
        {
            double clamped = Math.Max(MinMoveSpeed, Math.Min(MaxMoveSpeed, speed));
            if (Math.Abs(_moveSpeed - clamped) < 1e-6)
                return;

            _moveSpeed = clamped;
            MoveSpeedChanged?.Invoke(this, EventArgs.Empty);
        }

        public void Detach()
        {
            if (!_attached)
                return;

            _attached = false;
            _keysHeld.Clear();
            _isLooking = false;
            ReleaseMouse();

            _viewport.IsRotationEnabled = true;
            _viewport.IsZoomEnabled = true;
            _viewport.IsMoveEnabled = true;
            _viewport.IsPanEnabled = true;
            _viewport.PreviewKeyDown -= Viewport_PreviewKeyDown;
            _viewport.PreviewKeyUp -= Viewport_PreviewKeyUp;
            _viewport.KeyDown -= Viewport_BlockHelixKeyDown;
            _viewport.PreviewMouseRightButtonDown -= Viewport_PreviewMouseRightButtonDown;
            _viewport.PreviewMouseRightButtonUp -= Viewport_PreviewMouseRightButtonUp;
            _viewport.PreviewMouseMove -= Viewport_PreviewMouseMove;
            _viewport.PreviewMouseWheel -= Viewport_PreviewMouseWheel;
            _viewport.LostKeyboardFocus -= Viewport_LostKeyboardFocus;
            CompositionTarget.Rendering -= CompositionTarget_Rendering;
        }

        private void Viewport_LostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
        {
            _keysHeld.Clear();
        }

        private void Viewport_BlockHelixKeyDown(object sender, KeyEventArgs e)
        {
            if (IsMovementKey(e.Key))
                e.Handled = true;
        }

        private void Viewport_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (!IsMovementKey(e.Key))
                return;

            _keysHeld.Add(e.Key);
            e.Handled = true;
        }

        private void Viewport_PreviewKeyUp(object sender, KeyEventArgs e)
        {
            _keysHeld.Remove(e.Key);

            if (IsMovementKey(e.Key))
                e.Handled = true;
        }

        private void Viewport_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            _isLooking = true;
            _lastMouse = e.GetPosition(_viewport);
            _viewport.Focus();
            _viewport.CaptureMouse();
            e.Handled = true;
        }

        private void Viewport_PreviewMouseRightButtonUp(object sender, MouseButtonEventArgs e)
        {
            _isLooking = false;
            ReleaseMouse();
            e.Handled = true;
        }

        private void Viewport_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            double factor = e.Delta > 0 ? ScrollSpeedFactor : 1.0 / ScrollSpeedFactor;
            SetMoveSpeed(_moveSpeed * factor);
            e.Handled = true;
        }

        private void Viewport_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (!_isLooking)
                return;

            Point current = e.GetPosition(_viewport);
            double dx = current.X - _lastMouse.X;
            double dy = current.Y - _lastMouse.Y;
            _lastMouse = current;

            if (Math.Abs(dx) > double.Epsilon || Math.Abs(dy) > double.Epsilon)
                ApplyLookDelta(dx, dy);

            e.Handled = true;
        }

        private void CompositionTarget_Rendering(object sender, EventArgs e)
        {
            if (_keysHeld.Count == 0 || !_viewport.IsKeyboardFocusWithin)
                return;

            PerspectiveCamera camera = _viewport.Camera as PerspectiveCamera;
            if (camera == null)
                return;

            DateTime now = DateTime.UtcNow;
            double dt = (now - _lastMoveTime).TotalSeconds;
            _lastMoveTime = now;
            if (dt <= 0 || dt > 0.1)
                dt = 1.0 / 60.0;

            Vector3D move = BuildMoveVector(camera);
            if (move.LengthSquared < 1e-10)
                return;

            move.Normalize();
            double speed = MoveSpeed * dt;
            if (_keysHeld.Contains(Key.LeftShift) || _keysHeld.Contains(Key.RightShift))
                speed *= SprintMultiplier;

            move *= speed;
            // Translate position only — changing LookDirection during strafe tilts the view (feels like rotation).
            camera.Position += move;
        }

        private Vector3D BuildMoveVector(PerspectiveCamera camera)
        {
            GetCameraBasis(camera, out Vector3D forward, out Vector3D right, out _);

            Vector3D worldUp = new Vector3D(0, 1, 0);
            Vector3D move = new Vector3D();
            if (_keysHeld.Contains(Key.W))
                move += forward;
            if (_keysHeld.Contains(Key.S))
                move -= forward;
            if (_keysHeld.Contains(Key.D))
                move += right;
            if (_keysHeld.Contains(Key.A))
                move -= right;
            if (_keysHeld.Contains(Key.Space))
                move += worldUp;
            if (_keysHeld.Contains(Key.C))
                move -= worldUp;

            return move;
        }

        private static void GetCameraBasis(PerspectiveCamera camera, out Vector3D forward, out Vector3D right, out Vector3D up)
        {
            Vector3D worldUp = new Vector3D(0, 1, 0);

            forward = camera.LookDirection;
            if (forward.LengthSquared < 1e-10)
                forward = new Vector3D(0, 0, -1);
            forward.Normalize();

            right = Vector3D.CrossProduct(forward, worldUp);
            if (right.LengthSquared < 1e-10)
            {
                Vector3D fallback = camera.UpDirection;
                if (fallback.LengthSquared < 1e-10)
                    fallback = new Vector3D(1, 0, 0);
                fallback.Normalize();
                right = Vector3D.CrossProduct(forward, fallback);
            }

            if (right.LengthSquared < 1e-10)
                right = new Vector3D(1, 0, 0);
            right.Normalize();

            up = Vector3D.CrossProduct(right, forward);
            if (up.LengthSquared < 1e-10)
                up = worldUp;
            up.Normalize();
        }

        private void ApplyLookDelta(double dx, double dy)
        {
            PerspectiveCamera camera = _viewport.Camera as PerspectiveCamera;
            if (camera == null)
                return;

            double yawDegrees = -dx * LookSensitivity;
            double pitchDegrees = -dy * LookSensitivity;

            Vector3D look = camera.LookDirection;
            double distance = look.Length;
            if (distance < 1e-6)
                distance = 1.0;
            look.Normalize();

            Matrix3D yawRotation = Matrix3D.Identity;
            yawRotation.Rotate(new Quaternion(new Vector3D(0, 1, 0), yawDegrees));
            look = yawRotation.Transform(look);

            Vector3D right = Vector3D.CrossProduct(look, new Vector3D(0, 1, 0));
            if (right.LengthSquared < 1e-10)
                return;
            right.Normalize();

            Vector3D pitched = look;
            Matrix3D pitchRotation = Matrix3D.Identity;
            pitchRotation.Rotate(new Quaternion(right, pitchDegrees));
            pitched = pitchRotation.Transform(look);
            pitched.Normalize();

            double pitchAngle = Vector3D.AngleBetween(pitched, new Vector3D(0, 1, 0)) - 90.0;
            if (pitchAngle >= MinPitchDegrees && pitchAngle <= MaxPitchDegrees)
                look = pitched;

            camera.LookDirection = look * distance;
        }

        private void ReleaseMouse()
        {
            if (Mouse.Captured == _viewport)
                _viewport.ReleaseMouseCapture();
        }

        private static bool IsMovementKey(Key key)
        {
            switch (key)
            {
                case Key.W:
                case Key.A:
                case Key.S:
                case Key.D:
                case Key.Space:
                case Key.C:
                    return true;
                default:
                    return false;
            }
        }
    }
}

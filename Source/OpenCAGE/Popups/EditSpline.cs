using CATHODE.Scripting;
using CATHODE.Scripting.Internal;
using CathodeLib.ObjectExtensions;
using OpenCAGE.Popups.Base;
using OpenCAGE.Popups.UserControls;
using OpenCAGE.UnityConnection;
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Windows.Forms;

namespace OpenCAGE
{
    public partial class EditSpline : BaseWindow
    {
        public Action<cSpline> OnSaved;

        /* The window whose spline the viewport is editing (Edit in Viewport), if any - one at a time, and the viewer's
           replies go to it */
        private static EditSpline _inViewport;

        private GUI_SplineViewer splineViewer;
        private cSpline spline;
        private readonly Entity _entity;
        private readonly Composite _composite;
        private bool isClosedLoop = false; //Default loop val is false
        private bool _updatingList;
        private static readonly ShortGuid PointsParameter = ShortGuidUtils.Generate("points");

        public EditSpline(cSpline _spline, Parameter _closed, Entity entity = null, Composite composite = null) : base(WindowClosesOn.COMMANDS_RELOAD | WindowClosesOn.NEW_ENTITY_SELECTION | WindowClosesOn.NEW_COMPOSITE_SELECTION)
        {
            InitializeComponent();
            spline = _spline.Copy();
            _entity = entity;
            _composite = composite;
            if (_closed != null) isClosedLoop = ((cBool)_closed.content).value;

            splineViewer = new GUI_SplineViewer();
            modelRendererHost.Child = splineViewer;

            //The viewport edits the entity where it is selected, so it needs to know which one that is
            editInViewport.Enabled = _entity != null;
            pointTransform.OnValueChanged += PointEditedInFields;
            FormClosed += (s, e) => LeaveViewport();

            RefreshPointList(0, true);
        }

        private void UpdateSplineVisual(bool zoomExtents = false)
        {
            splineViewer.ShowSpline(spline, zoomExtents, isClosedLoop, splinePoints.SelectedIndex);
            SendToViewer();
        }

        /* The list holds a line per point; select is the point to select afterwards (out of range = the first, if any) */
        private void RefreshPointList(int select, bool zoomExtents = false)
        {
            _updatingList = true;
            splinePoints.BeginUpdate();
            splinePoints.Items.Clear();
            for (int i = 0; i < spline.splinePoints.Count; i++)
                splinePoints.Items.Add(DescribePoint(i));
            splinePoints.EndUpdate();
            splinePoints.SelectedIndex = select >= 0 && select < spline.splinePoints.Count ? select : (spline.splinePoints.Count > 0 ? 0 : -1);
            _updatingList = false;
            ShowSelection(zoomExtents);
        }

        private string DescribePoint(int index)
        {
            Vector3 position = spline.splinePoints[index].position;
            return index + "    (" + position.X.ToString("0.00") + ", " + position.Y.ToString("0.00") + ", " + position.Z.ToString("0.00") + ")";
        }

        private void UpdateListText(int index)
        {
            if (index < 0 || index >= splinePoints.Items.Count)
                return;
            _updatingList = true;
            int selected = splinePoints.SelectedIndex;
            splinePoints.Items[index] = DescribePoint(index);
            splinePoints.SelectedIndex = selected;
            _updatingList = false;
        }

        private void SelectPoint(int index)
        {
            _updatingList = true;
            splinePoints.SelectedIndex = index >= 0 && index < splinePoints.Items.Count ? index : -1;
            _updatingList = false;
            ShowSelection();
        }

        private void ShowSelection(bool zoomExtents = false)
        {
            int index = splinePoints.SelectedIndex;
            removePoint.Enabled = index != -1;
            insertPoint.Enabled = index != -1;
            pointTransform.Visible = index != -1;
            if (index != -1)
                pointTransform.PopulateUI(null, spline.splinePoints[index], "Spline Point " + index);
            UpdateSplineVisual(zoomExtents);
        }

        private void splinePoints_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (_updatingList)
                return;
            ShowSelection();
        }

        /* The position fields edit the selected point in place */
        private void PointEditedInFields()
        {
            UpdateListText(splinePoints.SelectedIndex);
            UpdateSplineVisual();
        }

        /* A new point carries on from the last by the same step - or a metre along X after a lone point */
        private void addPoint_Click(object sender, EventArgs e)
        {
            List<cTransform> points = spline.splinePoints;
            cTransform point = new cTransform();
            if (points.Count == 1)
            {
                point.position = points[0].position + new Vector3(1, 0, 0);
                point.rotation = points[0].rotation;
            }
            else if (points.Count > 1)
            {
                cTransform last = points[points.Count - 1];
                point.position = last.position + (last.position - points[points.Count - 2].position);
                point.rotation = last.rotation;
            }
            points.Add(point);
            RefreshPointList(points.Count - 1);
        }

        /* After the selected point: halfway to the next one, or carrying on past the last */
        private void insertPoint_Click(object sender, EventArgs e)
        {
            int index = splinePoints.SelectedIndex;
            List<cTransform> points = spline.splinePoints;
            if (index == -1)
                return;
            if (index == points.Count - 1)
            {
                addPoint_Click(sender, e);
                return;
            }

            cTransform point = new cTransform();
            point.position = (points[index].position + points[index + 1].position) * 0.5f;
            point.rotation = points[index].rotation;
            points.Insert(index + 1, point);
            RefreshPointList(index + 1);
        }

        private void removePoint_Click(object sender, EventArgs e)
        {
            RemovePoint(splinePoints.SelectedIndex);
        }

        private void RemovePoint(int index)
        {
            if (index < 0 || index >= spline.splinePoints.Count)
                return;
            spline.splinePoints.RemoveAt(index);
            RefreshPointList(Math.Min(index, spline.splinePoints.Count - 1));
        }

        private void saveSpline_Click(object sender, EventArgs e)
        {
            OnSaved?.Invoke(spline);
            this.Close();
        }

        /* Edit in Viewport: the viewport draws this window's working points on the SplinePath where it is selected, and
           its gizmo moves the selected one. Like Animation Mode, nothing is written to the entity until Save - leaving the
           mode (or closing the window) puts the saved spline back on screen. */
        private void editInViewport_CheckedChanged(object sender, EventArgs e)
        {
            if (!editInViewport.Checked)
            {
                LeaveViewport();
                return;
            }

            if (!Send.Connected)
            {
                MessageBox.Show("The viewport isn't running, so there is nothing to edit the spline in.", "Edit in Viewport", MessageBoxButtons.OK, MessageBoxIcon.Information);
                editInViewport.Checked = false;
                return;
            }

            if (_inViewport != null && _inViewport != this)
                _inViewport.LeaveViewport();
            _inViewport = this;
            if (RuntimeUtilsConnection.LiveLink.Connected && !viewportHint.Text.Contains("Live Link"))
                viewportHint.Text += " With Live Link on, the running game follows your edits too.";
            SendToViewer();
        }

        private void LeaveViewport()
        {
            if (_inViewport != this)
                return;
            _inViewport = null;
            Send.SendSplineEdit(false, default(ShortGuid), null, false, -1);
            //The running game goes back to the spline as the level has it (as saved, if this was a Save)
            RuntimeUtilsConnection.LiveLink.ClearPreview(_composite, _entity, PointsParameter);
            if (!IsDisposed && editInViewport.Checked)
                editInViewport.Checked = false;
        }

        private void SendToViewer()
        {
            if (_inViewport != this || _entity == null)
                return;
            Send.SendSplineEdit(true, _entity.shortGUID, spline.splinePoints, isClosedLoop, splinePoints.SelectedIndex);

            //Live Link: the running game follows the working points too, without them being written to the level
            if (_composite != null && RuntimeUtilsConnection.LiveLink.Connected)
                RuntimeUtilsConnection.LiveLink.SetPreview(_composite, _entity, PointsParameter, spline);
        }

        /// <summary>The viewer rebuilt its scene or restarted: show it the spline being edited again, if there is one.</summary>
        public static void ResendToViewer()
        {
            if (_inViewport != null && !_inViewport.IsDisposed)
                _inViewport.SendToViewer();
        }

        /// <summary>A reply from the viewport (SPLINE_EDIT_*), for the window it is editing. UI thread.</summary>
        public static void ApplyViewerPacket(Packet packet)
        {
            EditSpline window = _inViewport;
            if (window == null || window.IsDisposed || window._entity == null || packet.spline_edit_entity != window._entity.shortGUID.AsUInt32)
                return;
            window.ApplyFromViewer(packet);
        }

        private void ApplyFromViewer(Packet packet)
        {
            int index = packet.spline_edit_selected;
            switch (packet.packet_event)
            {
                case PacketEvent.SPLINE_EDIT_POINT_PICKED:
                    SelectPoint(index);
                    break;
                case PacketEvent.SPLINE_EDIT_POINT_MOVED:
                    if (index < 0 || index >= spline.splinePoints.Count || packet.spline_edit_points == null || packet.spline_edit_points.Count == 0)
                        return;
                    cTransform point = spline.splinePoints[index];
                    point.position = ToVector(packet.spline_edit_points[0].position);
                    point.rotation = ToVector(packet.spline_edit_points[0].rotation);
                    UpdateListText(index);
                    if (index == splinePoints.SelectedIndex)
                        pointTransform.SetTransformValues(point);
                    UpdateSplineVisual();
                    break;
                case PacketEvent.SPLINE_EDIT_POINT_DELETE_REQUEST:
                    RemovePoint(index);
                    break;
                case PacketEvent.SPLINE_EDIT_EXIT_REQUEST:
                    LeaveViewport();
                    break;
            }
        }

        private static Vector3 ToVector(float[] values)
        {
            if (values == null || values.Length < 3)
                return new Vector3();
            return new Vector3(values[0], values[1], values[2]);
        }
    }
}

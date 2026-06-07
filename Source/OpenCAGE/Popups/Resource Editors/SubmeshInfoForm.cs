using CATHODE;
using CathodeLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;

namespace OpenCAGE
{
    public partial class SubmeshInfoForm : Form
    {
        public static void ShowFor(IWin32Window owner, Models.CS2.Component.LOD.Submesh submesh)
        {
            using (var f = new SubmeshInfoForm(submesh))
                f.ShowDialog(owner);
        }

        public SubmeshInfoForm(Models.CS2.Component.LOD.Submesh submesh)
        {
            InitializeComponent();
            ApplySubmesh(submesh);
        }

        private void ApplySubmesh(Models.CS2.Component.LOD.Submesh submesh)
        {
            int vertices = 0;
            int indices = 0;
            int triangles = 0;

            lblDecodeError.Visible = false;
            lblDecodeError.Text = string.Empty;

            if (submesh != null)
            {
                try
                {
                    cMesh mesh = ModelUtility.ToMesh(submesh);
                    vertices = mesh.Vertices?.Count ?? 0;
                    indices = mesh.Indices?.Count ?? 0;
                    triangles = indices / 3;
                }
                catch (Exception ex)
                {
                    lblDecodeError.Text = ex.Message;
                    lblDecodeError.Visible = true;
                }
            }

            lblVertexCount.Text = vertices.ToString("N0");
            lblIndexCount.Text = indices.ToString("N0");
            lblTriangleCount.Text = triangles.ToString("N0");
            lblSupportedValue.Text = CollectVertexFormatUsages(submesh?.VertexFormatFull);
        }

        private static string CollectVertexFormatUsages(Models.VertexFormat vf)
        {
            if (vf?.Attributes == null || vf.Attributes.Count == 0)
                return "(none)";

            var usages = new HashSet<Models.VertexFormat.Usage>();
            int vertexStreamEnd = vf.Attributes.Count - 1;
            for (int stream = 0; stream < vertexStreamEnd; stream++)
            {
                List<Models.VertexFormat.Attribute> slots = vf.Attributes[stream];
                if (slots == null)
                    continue;
                foreach (Models.VertexFormat.Attribute attr in slots)
                {
                    if (attr == null)
                        continue;
                    if (attr.Type == Models.VertexFormat.Type.Unused)
                        continue;
                    usages.Add(attr.Usage);
                }
            }

            if (usages.Count == 0)
                return "(none)";

            return string.Join(", ", usages.OrderBy(u => u.ToString()).Select(u => u.ToString()));
        }
    }
}

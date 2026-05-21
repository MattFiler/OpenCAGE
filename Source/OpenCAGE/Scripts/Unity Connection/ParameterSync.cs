using CATHODE;
using CATHODE.Scripting;
using CATHODE.Scripting.Internal;
using CathodeLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace OpenCAGE.UnityConnection
{
    /// <summary>
    /// Platform-neutral parameter payload for websocket sync. Keep in sync with Unity ParameterSync.cs.
    /// </summary>
    public class RenderableSyncElement
    {
        public int model_index = -1;
        public int material_index = -1;
    }

    public class SyncedParameter
    {
        public uint name;
        public bool removed;
        public uint data_type;

        public float[] vector3_a;
        public float[] vector3_b;

        public bool bool_value;
        public int int_value;
        public float float_value;
        public string string_value;

        public uint enum_id;
        public int enum_index;

        public List<RenderableSyncElement> renderable = new List<RenderableSyncElement>();
        public int renderable_reds_index = -1;
        public int renderable_reds_count = 0;
        public List<float[]> spline_points = new List<float[]>();
    }

    public static class ParameterSync
    {
        public static SyncedParameter Pack(Parameter parameter, LevelContent content, bool removed = false)
        {
            if (parameter == null)
                return null;

            bool isRemoved = removed || parameter.content == null;
            SyncedParameter sync = new SyncedParameter()
            {
                name = parameter.name.AsUInt32,
                removed = isRemoved,
                data_type = parameter.content != null
                    ? (uint)parameter.content.dataType
                    : (uint)InferDataType(parameter.name),
            };

            if (parameter.content == null)
                return sync;

            switch (parameter.content.dataType)
            {
                case DataType.TRANSFORM:
                    cTransform transform = (cTransform)parameter.content;
                    sync.vector3_a = ToArray(transform.position);
                    sync.vector3_b = ToArray(transform.rotation);
                    break;
                case DataType.VECTOR:
                    sync.vector3_a = ToArray(((cVector3)parameter.content).value);
                    break;
                case DataType.BOOL:
                    sync.bool_value = ((cBool)parameter.content).value;
                    break;
                case DataType.INTEGER:
                    sync.int_value = ((cInteger)parameter.content).value;
                    break;
                case DataType.FLOAT:
                    sync.float_value = ((cFloat)parameter.content).value;
                    break;
                case DataType.STRING:
                    sync.string_value = ((cString)parameter.content).value;
                    break;
                case DataType.ENUM:
                    cEnum enumVal = (cEnum)parameter.content;
                    sync.enum_id = enumVal.enumID.AsUInt32;
                    sync.enum_index = enumVal.enumIndex;
                    break;
                case DataType.ENUM_STRING:
                    cEnumString enumString = (cEnumString)parameter.content;
                    sync.enum_id = enumString.enumID.AsUInt32;
                    sync.string_value = enumString.value;
                    break;
                case DataType.RESOURCE:
                    PackResource(sync, (cResource)parameter.content, content);
                    break;
                case DataType.SPLINE:
                    foreach (cTransform point in ((cSpline)parameter.content).splinePoints)
                        sync.spline_points.Add(ToArray(point.position).Concat(ToArray(point.rotation)).ToArray());
                    break;
            }

            return sync;
        }

        private static void PackResource(SyncedParameter sync, cResource resource, LevelContent content)
        {
            if (content?.Level == null || resource == null)
                return;

            ResourceReference renderable = resource.GetResource(ResourceType.RENDERABLE_INSTANCE);
            if (renderable == null)
                return;

            for (int i = 0; i < renderable.RenderableInstance.Count; i++)
            {
                int modelIndex = content.Level.Models.GetWriteIndex(renderable.RenderableInstance[i].Model);
                int materialIndex = content.Level.Materials.GetWriteIndex(renderable.RenderableInstance[i].Material);
                if (modelIndex < 0 || materialIndex < 0)
                    continue;

                sync.renderable.Add(new RenderableSyncElement()
                {
                    model_index = modelIndex,
                    material_index = materialIndex,
                });
            }

            if (sync.renderable.Count == 0 && renderable.RenderableInstance.Count > 0)
            {
                int redsIndex = content.Level.RenderableElements.GetWriteIndex(renderable.RenderableInstance);
                if (redsIndex >= 0)
                {
                    sync.renderable_reds_index = redsIndex;
                    sync.renderable_reds_count = renderable.RenderableInstance.Count;
                }
            }
        }

        private static float[] ToArray(Vector3 vector)
        {
            return new float[] { vector.X, vector.Y, vector.Z };
        }

        private static DataType InferDataType(ShortGuid name)
        {
            if (name == ShortGuidUtils.Generate("resource"))
                return DataType.RESOURCE;
            if (name == ShortGuidUtils.Generate("position"))
                return DataType.TRANSFORM;
            if (name == ShortGuidUtils.Generate("half_dimensions"))
                return DataType.VECTOR;
            return DataType.NONE;
        }
    }
}

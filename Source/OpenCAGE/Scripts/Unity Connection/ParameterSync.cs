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
                    if (parameter.name == ShortGuidUtils.Generate("mapping"))
                    {
                        cResource mapping = (cResource)parameter.content;
                        sync.enum_id = mapping?.shortGUID.AsUInt32 ?? 0;
                    }
                    else
                    {
                        PackResource(sync, (cResource)parameter.content, content);
                    }
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

        public static DataType GetDataType(SyncedParameter sync)
        {
            if (sync == null)
                return DataType.NONE;

            DataType dataType = (DataType)sync.data_type;
            if (dataType != DataType.NONE)
                return dataType;

            return InferDataType(new ShortGuid(sync.name));
        }

        public static void ApplyToEntity(Entity entity, SyncedParameter sync, LevelContent content)
        {
            if (entity == null || sync == null)
                return;

            ShortGuid paramName = new ShortGuid(sync.name);
            if (sync.removed)
            {
                entity.RemoveParameter(paramName);
                return;
            }

            ParameterData data = Unpack(sync, content);
            if (data == null)
                return;

            Parameter existing = entity.GetParameter(paramName);
            if (existing == null || existing.content?.dataType != data.dataType)
                entity.AddParameter(paramName, data);
            else
                existing.content = data;
        }

        public static ParameterData Unpack(SyncedParameter sync, LevelContent content = null)
        {
            DataType dataType = GetDataType(sync);
            switch (dataType)
            {
                case DataType.TRANSFORM:
                    return new cTransform(ToVector3(sync.vector3_a), ToVector3(sync.vector3_b));
                case DataType.VECTOR:
                    return new cVector3(ToVector3(sync.vector3_a));
                case DataType.BOOL:
                    return new cBool(sync.bool_value);
                case DataType.INTEGER:
                    return new cInteger(sync.int_value);
                case DataType.FLOAT:
                    return new cFloat(sync.float_value);
                case DataType.STRING:
                    return new cString(sync.string_value ?? "");
                case DataType.ENUM:
                    return new cEnum(new ShortGuid(sync.enum_id), sync.enum_index);
                case DataType.ENUM_STRING:
                    return new cEnumString(new ShortGuid(sync.enum_id), sync.string_value ?? "");
                case DataType.RESOURCE:
                    if (new ShortGuid(sync.name) == ShortGuidUtils.Generate("mapping"))
                        return UnpackMappingResource(sync);
                    return UnpackResource(sync, content);
                case DataType.SPLINE:
                    List<cTransform> points = new List<cTransform>();
                    if (sync.spline_points != null)
                    {
                        foreach (float[] point in sync.spline_points)
                        {
                            if (point == null || point.Length < 6)
                                continue;
                            points.Add(new cTransform(ToVector3(point, 0), ToVector3(point, 3)));
                        }
                    }
                    return new cSpline(points);
                default:
                    return null;
            }
        }

        private static cResource UnpackMappingResource(SyncedParameter sync)
        {
            ShortGuid mappingId = new ShortGuid(sync.enum_id);
            return mappingId == ShortGuid.Invalid ? new cResource() : new cResource(mappingId);
        }

        private static cResource UnpackResource(SyncedParameter sync, LevelContent content)
        {
            if (content?.Level == null)
                return null;

            List<Tuple<int, int>> renderables = ToRenderableIndexList(sync, content);
            if (renderables.Count == 0)
                return null;

            cResource resource = new cResource(ResourceType.RENDERABLE_INSTANCE);
            ResourceReference renderable = resource.AddResource(ResourceType.RENDERABLE_INSTANCE);
            foreach (Tuple<int, int> element in renderables)
            {
                Models.CS2.Component.LOD.Submesh model = content.Level.Models.GetAtWriteIndex(element.Item1);
                Materials.Material material = content.Level.Materials.GetAtWriteIndex(element.Item2);
                if (model == null || material == null)
                    continue;

                renderable.RenderableInstance.Add(new RenderableElements.Element()
                {
                    Model = model,
                    Material = material,
                });
            }
            return resource;
        }

        public static List<Tuple<int, int>> ToRenderableIndexList(SyncedParameter sync, LevelContent content = null)
        {
            List<Tuple<int, int>> list = new List<Tuple<int, int>>();
            if (sync?.renderable != null)
            {
                foreach (RenderableSyncElement element in sync.renderable)
                {
                    if (element == null || element.model_index < 0 || element.material_index < 0)
                        continue;
                    list.Add(new Tuple<int, int>(element.model_index, element.material_index));
                }
            }

            if (list.Count == 0 && content?.Level != null && sync.renderable_reds_index >= 0 && sync.renderable_reds_count > 0)
            {
                List<RenderableElements.Element> elements = content.Level.RenderableElements.GetAtWriteIndex(sync.renderable_reds_index, sync.renderable_reds_count);
                for (int i = 0; i < elements.Count; i++)
                {
                    int modelIndex = content.Level.Models.GetWriteIndex(elements[i].Model);
                    int materialIndex = content.Level.Materials.GetWriteIndex(elements[i].Material);
                    if (modelIndex < 0 || materialIndex < 0)
                        continue;
                    list.Add(new Tuple<int, int>(modelIndex, materialIndex));
                }
            }
            return list;
        }

        private static Vector3 ToVector3(float[] values)
        {
            if (values == null || values.Length < 3)
                return Vector3.Zero;
            return new Vector3(values[0], values[1], values[2]);
        }

        private static Vector3 ToVector3(float[] values, int offset)
        {
            if (values == null || values.Length < offset + 3)
                return Vector3.Zero;
            return new Vector3(values[offset], values[offset + 1], values[offset + 2]);
        }

        private static DataType InferDataType(ShortGuid name)
        {
            if (name == ShortGuidUtils.Generate("resource"))
                return DataType.RESOURCE;
            if (name == ShortGuidUtils.Generate("mapping"))
                return DataType.RESOURCE;
            if (name == ShortGuidUtils.Generate("position"))
                return DataType.TRANSFORM;
            if (name == ShortGuidUtils.Generate("half_dimensions"))
                return DataType.VECTOR;
            if (name == ShortGuidUtils.Generate("points"))
                return DataType.SPLINE;
            if (name == ShortGuidUtils.Generate("loop"))
                return DataType.BOOL;
            return DataType.NONE;
        }
    }
}

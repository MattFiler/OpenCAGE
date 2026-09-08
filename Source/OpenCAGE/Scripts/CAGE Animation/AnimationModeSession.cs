using CATHODE;
using CATHODE.Scripting;
using CATHODE.Scripting.Internal;
using CathodeLib;
using OpenCAGE.UnityConnection;
using System;
using System.Collections.Generic;
using System.Numerics;

namespace OpenCAGE
{
    /// <summary>What Animation Mode needs from the CAGEAnimation editor window that owns it.</summary>
    public interface IAnimationModeHost
    {
        /// <summary>The animation being edited - the editor's live working copy, not the saved entity.</summary>
        CAGEAnimation Animation { get; }

        /// <summary>The composite the animation lives in, which its connection paths are written against.</summary>
        Composite AnimationComposite { get; }

        LevelContent AnimationContent { get; }

        /// <summary>The playable length, which is where a newly added track's closing keyframe goes.</summary>
        float AnimationLength { get; }

        /// <summary>Interpolation as the graph is drawing it, so a preview matches what is on screen.</summary>
        bool BezierInterpolation { get; }

        /// <summary>
        /// Tracks or keyframes were changed from outside the window - a viewport move, an inspector
        /// edit. The window rebuilds what it shows and writes the change back to the entity.
        /// </summary>
        /// <param name="structureChanged">
        /// True when the animation gained a track it did not have. A gizmo drag sends a stream of
        /// keyframe values and rebuilding the whole graph for each of them would make it unusable, so
        /// the window only does the expensive rebuild when the tracks themselves have changed.
        /// </param>
        void OnAnimationEditedExternally(string undoLabel, bool structureChanged);
    }

    /// <summary>
    /// Animation Mode: the CAGEAnimation editor driving the Level Viewer from its playhead, and taking
    /// edits made out in the world back as keyframes.
    /// </summary>
    /// <remarks>
    /// Two directions, and the important thing about both is that the level's own data is never
    /// touched:
    ///
    /// - Outwards, the animation is evaluated at the playhead and the answers are sent as a transient
    ///   override the viewer paints on top of its scene (<see cref="PacketEvent.ANIMATION_PREVIEW"/>).
    ///   Leaving the mode clears it and everything goes back where it was.
    /// - Inwards, moving something in the viewport (or editing a parameter in the inspector) while the
    ///   mode is on is how keyframes are made. The edit does not become the entity's value - it becomes
    ///   a keyframe at the playhead, and the entity keeps the value it rests at when nothing is
    ///   playing.
    ///
    /// Targets are addressed by instance path from the composite the viewer populated, because a
    /// connection path is written relative to the composite the animation lives in and only names a
    /// particular thing once you know which placement of that composite you are standing in.
    /// </remarks>
    public sealed class AnimationModeSession
    {
        /// <summary>The transform sub-properties a position parameter is animated through.</summary>
        private static readonly string[] TransformSubProperties = new string[] { "x", "y", "z", "Yaw", "Pitch", "Roll" };

        public static AnimationModeSession Current { get; private set; }
        public static bool Active => Current != null;

        /// <summary>
        /// The animation started (or stopped) driving something it wasn't before, so the inspector's
        /// purple highlights have moved. Raised only for that - not for every keyframe value, which a
        /// gizmo drag produces by the dozen and none of which change what is driven.
        /// </summary>
        public static event Action DriversChanged;

        private readonly IAnimationModeHost _host;
        private readonly Composite _rootComposite;
        private readonly List<uint> _drillPath;

        public float Time { get; private set; }

        private AnimationModeSession(IAnimationModeHost host, Composite rootComposite, List<uint> drillPath)
        {
            _host = host;
            _rootComposite = rootComposite;
            _drillPath = drillPath ?? new List<uint>();
        }

        private Commands Commands => _host?.AnimationContent?.Level?.Commands;

        #region LIFETIME

        /// <summary>
        /// Turn Animation Mode on for a CAGEAnimation editor window. <paramref name="drillPath"/> is
        /// the hierarchy the user walked through to reach the animation's composite, which is what
        /// decides which instance of everything it drives is the one being previewed.
        /// </summary>
        public static AnimationModeSession Begin(IAnimationModeHost host, Composite rootComposite, List<uint> drillPath)
        {
            if (host == null)
                return null;

            End();
            Current = new AnimationModeSession(host, rootComposite, drillPath);
            Current.PushPreview();
            return Current;
        }

        /// <summary>Turn it off, putting everything the preview moved back where it was.</summary>
        public static void End()
        {
            if (Current == null)
                return;

            Current = null;
            Send.SendAnimationPreviewCleared();
        }

        /// <summary>End the mode if it belongs to this window (it may already have been replaced).</summary>
        public static void EndFor(IAnimationModeHost host)
        {
            if (Current != null && ReferenceEquals(Current._host, host))
                End();
        }

        /// <summary>
        /// A CAGEAnimation was edited, so what the level animates has moved. Drops the cached index and
        /// tells the inspector to repaint its purple rows - said here rather than by each editor so
        /// there is one place that knows what an animation edit means to the rest of the UI.
        /// </summary>
        public static void NotifyAnimationEdited()
        {
            CageAnimationDrivers.Invalidate();
            DriversChanged?.Invoke();
        }

        /// <summary>
        /// An undo or redo put different track lists on a CAGEAnimation. An editor window open on it is
        /// holding a working copy of the lists that were just replaced, and its next save would write
        /// them straight back over the top - so it is told to take the new ones instead.
        /// </summary>
        public static event Action<CAGEAnimation> AnimationReplaced;

        public static void NotifyAnimationReplaced(CAGEAnimation animation)
        {
            AnimationReplaced?.Invoke(animation);
        }

        #endregion

        #region PLAYHEAD

        public void SetTime(float time)
        {
            if (time < 0f) time = 0f;
            if (Math.Abs(Time - time) <= 1e-5f)
                return;
            Time = time;
            PushPreview();
        }

        /// <summary>Re-evaluate and re-send: the tracks changed under a playhead that did not move.</summary>
        public void Refresh()
        {
            PushPreview();
        }

        #endregion

        #region PREVIEW (editor -> viewer)

        private void PushPreview()
        {
            List<SyncedAnimationTarget> targets = BuildPreviewTargets();
            Send.SendAnimationPreviewPacket(Time, targets);
        }

        /// <summary>
        /// Every entity instance the animation drives a transform on, with the transform it holds at
        /// the playhead. Only transforms: they are the one thing the viewer can show without being
        /// given the level data to write, which Animation Mode must not do.
        /// </summary>
        private List<SyncedAnimationTarget> BuildPreviewTargets()
        {
            List<SyncedAnimationTarget> targets = new List<SyncedAnimationTarget>();
            CAGEAnimation animation = _host?.Animation;
            Commands commands = Commands;
            Composite composite = _host?.AnimationComposite;
            if (animation == null || commands == null || composite == null)
                return targets;

            ShortGuid positionParam = ShortGuidUtils.Generate("position");
            bool bezier = _host.BezierInterpolation;

            //One entry per animated instance: a transform is driven through six separate tracks
            Dictionary<string, SyncedAnimationTarget> byPath = new Dictionary<string, SyncedAnimationTarget>();

            foreach (CAGEAnimation.Connection connection in animation.connections)
            {
                if (connection == null || connection.target_param != positionParam)
                    continue;

                CAGEAnimation.FloatTrack track = FindTrack(animation, connection.target_track);
                if (track == null)
                    continue;

                if (!TryResolveTargetPath(connection.connectedEntity, out List<uint> instancePath))
                    continue;

                string key = PathKey(instancePath);
                if (!byPath.TryGetValue(key, out SyncedAnimationTarget target))
                {
                    //Start from where the entity actually rests, so sub-properties with no track stay put
                    ReadBaseTransform(connection.connectedEntity, out Vector3 position, out Vector3 rotation);
                    target = new SyncedAnimationTarget()
                    {
                        path = instancePath,
                        parameter = positionParam.AsUInt32,
                        data_type = (uint)DataType.TRANSFORM,
                        vector3_a = new float[] { position.X, position.Y, position.Z },
                        vector3_b = new float[] { rotation.X, rotation.Y, rotation.Z },
                    };
                    byPath.Add(key, target);
                    targets.Add(target);
                }

                float value = CageAnimationCurves.ValueAt(track, Time, bezier);
                WriteTransformComponent(target, connection.target_sub_param, value);
            }

            return targets;
        }

        private static void WriteTransformComponent(SyncedAnimationTarget target, ShortGuid subProperty, float value)
        {
            for (int i = 0; i < TransformSubProperties.Length; i++)
            {
                if (ShortGuidUtils.Generate(TransformSubProperties[i]) != subProperty)
                    continue;

                switch (TransformSubProperties[i])
                {
                    case "x": target.vector3_a[0] = value; break;
                    case "y": target.vector3_a[1] = value; break;
                    case "z": target.vector3_a[2] = value; break;
                    //Cathode eulers: Y is yaw, X is pitch, Z is roll
                    case "Yaw": target.vector3_b[1] = value; break;
                    case "Pitch": target.vector3_b[0] = value; break;
                    case "Roll": target.vector3_b[2] = value; break;
                }
                return;
            }
        }

        #endregion

        #region RECORDING (viewer / inspector -> keyframes)

        /// <summary>
        /// An entity instance was moved out in the world while the mode was on. Keyframe it at the
        /// playhead - adding it to the animation if it was not in it - and answer true, which is what
        /// tells the caller not to write the move to the entity itself.
        /// </summary>
        public bool TryRecordTransform(IReadOnlyList<uint> instancePath, cTransform transform)
        {
            if (transform == null)
                return false;
            if (!TryStoredPathFor(instancePath, out EntityPath stored))
                return false;

            //Cathode eulers: Y is yaw, X is pitch, Z is roll - the order the sub-properties are in
            float[] values = new float[]
            {
                transform.position.X, transform.position.Y, transform.position.Z,
                transform.rotation.Y, transform.rotation.X, transform.rotation.Z,
            };

            bool changed = false;
            bool added = false;
            for (int i = 0; i < TransformSubProperties.Length; i++)
                changed |= RecordOne(stored, ShortGuidUtils.Generate("position"), DataType.TRANSFORM,
                    TransformSubProperties[i], values[i], ref added);

            if (changed)
                Committed("Keyframe " + DescribeTarget(stored), added);
            return changed;
        }

        /// <summary>
        /// A parameter was edited in the inspector while the mode was on. FLOAT and TRANSFORM are the
        /// data types a CAGEAnimation can drive; anything else is left as an ordinary edit.
        /// </summary>
        public bool TryRecordParameter(IReadOnlyList<uint> instancePath, Parameter parameter)
        {
            if (parameter?.content == null)
                return false;

            switch (parameter.content.dataType)
            {
                case DataType.TRANSFORM:
                    return TryRecordTransform(instancePath, (cTransform)parameter.content);
                case DataType.FLOAT:
                {
                    if (!TryStoredPathFor(instancePath, out EntityPath stored))
                        return false;
                    bool added = false;
                    if (!RecordOne(stored, parameter.name, DataType.FLOAT, "", ((cFloat)parameter.content).value, ref added))
                        return false;
                    Committed("Keyframe " + DescribeTarget(stored), added);
                    return true;
                }
                default:
                    return false;
            }
        }

        /* Put one value on one track, making the connection and the track first if the animation does
           not have them yet. A track that is being added now is seeded at the value the entity rests
           at, across the whole playable length, so the only thing that moves is the keyframe just made. */
        private bool RecordOne(EntityPath stored, ShortGuid parameter, DataType dataType, string subProperty, float value, ref bool addedConnection)
        {
            CAGEAnimation animation = _host?.Animation;
            if (animation == null)
                return false;

            ShortGuid subParam = ShortGuidUtils.Generate(subProperty);
            CAGEAnimation.Connection connection = null;
            foreach (CAGEAnimation.Connection candidate in animation.connections)
            {
                if (candidate == null || candidate.target_param != parameter || candidate.target_sub_param != subParam)
                    continue;
                if (candidate.connectedEntity != stored)
                    continue;
                if (FindTrack(animation, candidate.target_track) == null)
                    continue;
                connection = candidate;
                break;
            }

            if (connection == null)
            {
                connection = AddConnection(animation, stored, parameter, dataType, subProperty);
                addedConnection = true;
            }
            if (connection == null)
                return false;

            CAGEAnimation.FloatTrack track = FindTrack(animation, connection.target_track);
            return CageAnimationCurves.SetKeyframe(track, Time, value, InterpolationMode);
        }

        private CAGEAnimation.Connection AddConnection(
            CAGEAnimation animation, EntityPath stored, ShortGuid parameter, DataType dataType, string subProperty)
        {
            float resting = ReadBaseValue(stored, parameter, subProperty);

            CAGEAnimation.FloatTrack track = new CAGEAnimation.FloatTrack()
            {
                shortGUID = ShortGuidUtils.GenerateRandom(),
            };
            track.keyframes.Add(NewKeyframe(0f, resting));
            track.keyframes.Add(NewKeyframe(Math.Max(_host.AnimationLength, 0.01f), resting));
            animation.floatTracks.Add(track);

            CAGEAnimation.Connection connection = new CAGEAnimation.Connection()
            {
                binding_guid = ShortGuidUtils.GenerateRandom(),
                binding_type = ObjectType.ENTITY,
                target_param = parameter,
                target_param_type = dataType,
                target_sub_param = ShortGuidUtils.Generate(subProperty),
                target_track = track.shortGUID,
            };
            connection.connectedEntity.path = stored.path;
            animation.connections.Add(connection);
            return connection;
        }

        private CAGEAnimation.FloatTrack.Keyframe NewKeyframe(float time, float value)
        {
            return CageAnimationCurves.NewKeyframe(time, value, InterpolationMode);
        }

        private CAGEAnimation.InterpolationMode InterpolationMode =>
            _host.BezierInterpolation ? CAGEAnimation.InterpolationMode.Bezier : CAGEAnimation.InterpolationMode.Linear;

        private void Committed(string undoLabel, bool driversChanged)
        {
            _host.OnAnimationEditedExternally(undoLabel, driversChanged);
            PushPreview();
            if (driversChanged)
                DriversChanged?.Invoke();
        }

        #endregion

        #region PATHS

        /* Where this connection's target is, as an instance path from the composite the viewer
           populated - or nothing, if it names somewhere this hierarchy cannot reach. */
        private bool TryResolveTargetPath(EntityPath stored, out List<uint> instancePath)
        {
            instancePath = null;
            Commands commands = Commands;
            Composite composite = _host?.AnimationComposite;
            if (commands == null || composite == null)
                return false;

            if (!EntityInstancePath.TryResolve(commands, composite, _drillPath, stored, out instancePath, out bool relativeToHere))
                return false;

            //A path written from the level root names somewhere a hierarchy starting elsewhere cannot place
            Composite levelRoot = commands.EntryPoints != null && commands.EntryPoints.Length != 0 ? commands.EntryPoints[0] : null;
            if (!relativeToHere && _rootComposite != levelRoot)
                return false;

            return true;
        }

        /* The other direction: an instance path the viewer named, as a path the animation can store.
           Something outside the animation's own composite cannot be addressed from it, so it is refused
           rather than guessed at. */
        private bool TryStoredPathFor(IReadOnlyList<uint> instancePath, out EntityPath stored)
        {
            stored = null;
            if (instancePath == null || instancePath.Count == 0)
                return false;

            //Already animated: keep the exact path the animation stores, so the same track is found again
            CAGEAnimation animation = _host?.Animation;
            if (animation != null)
            {
                foreach (CAGEAnimation.Connection connection in animation.connections)
                {
                    if (connection == null)
                        continue;
                    if (TryResolveTargetPath(connection.connectedEntity, out List<uint> resolved)
                        && EntityInstancePath.Equal(resolved, instancePath))
                    {
                        stored = connection.connectedEntity;
                        return true;
                    }
                }
            }

            if (instancePath.Count <= _drillPath.Count)
                return false;
            for (int i = 0; i < _drillPath.Count; i++)
            {
                if (instancePath[i] != _drillPath[i])
                    return false;
            }

            ShortGuid[] path = new ShortGuid[instancePath.Count - _drillPath.Count];
            for (int i = 0; i < path.Length; i++)
                path[i] = new ShortGuid(instancePath[_drillPath.Count + i]);

            //Must actually resolve from the animation's composite, or the animation would store a path to nowhere
            EntityPath candidate = new EntityPath(path);
            List<Tuple<Composite, Entity>> resolvedPath = Commands?.Utils?.ResolveEntityPath(candidate, _host.AnimationComposite);
            if (resolvedPath == null || resolvedPath.Count == 0)
                return false;

            stored = candidate;
            return true;
        }

        private static string PathKey(IReadOnlyList<uint> instancePath)
        {
            System.Text.StringBuilder builder = new System.Text.StringBuilder(instancePath.Count * 9);
            for (int i = 0; i < instancePath.Count; i++)
            {
                if (i != 0) builder.Append('/');
                builder.Append(instancePath[i]);
            }
            return builder.ToString();
        }

        #endregion

        #region TARGET VALUES

        /* The transform the target rests at when nothing is animating it */
        private void ReadBaseTransform(EntityPath stored, out Vector3 position, out Vector3 rotation)
        {
            position = Vector3.Zero;
            rotation = Vector3.Zero;

            cTransform transform = ResolveParameter(stored, ShortGuidUtils.Generate("position"))?.content as cTransform;
            if (transform == null)
                return;
            position = transform.position;
            rotation = transform.rotation;
        }

        private float ReadBaseValue(EntityPath stored, ShortGuid parameter, string subProperty)
        {
            ParameterData content = ResolveParameter(stored, parameter)?.content;
            if (content == null)
                return 0f;

            switch (content.dataType)
            {
                case DataType.FLOAT:
                    return ((cFloat)content).value;
                case DataType.TRANSFORM:
                {
                    cTransform transform = (cTransform)content;
                    switch (subProperty)
                    {
                        case "x": return transform.position.X;
                        case "y": return transform.position.Y;
                        case "z": return transform.position.Z;
                        case "Yaw": return transform.rotation.Y;
                        case "Pitch": return transform.rotation.X;
                        case "Roll": return transform.rotation.Z;
                    }
                    return 0f;
                }
            }
            return 0f;
        }

        private Parameter ResolveParameter(EntityPath stored, ShortGuid parameter)
        {
            Commands commands = Commands;
            Composite composite = _host?.AnimationComposite;
            if (commands?.Utils == null || composite == null)
                return null;

            try
            {
                Entity entity = commands.Utils.GetResolvedTarget(commands.Utils.ResolveEntityPath(stored, composite)).Item2;
                return entity?.GetParameter(parameter);
            }
            catch
            {
                return null;
            }
        }

        private string DescribeTarget(EntityPath stored)
        {
            Commands commands = Commands;
            Composite composite = _host?.AnimationComposite;
            if (commands?.Utils == null || composite == null)
                return "entity";

            try
            {
                (Composite resolvedComposite, Entity entity) = commands.Utils.GetResolvedTarget(
                    commands.Utils.ResolveEntityPath(stored, composite));
                if (entity == null)
                    return "entity";
                return commands.Utils.GetEntityName(resolvedComposite ?? composite, entity);
            }
            catch
            {
                return "entity";
            }
        }

        private static CAGEAnimation.FloatTrack FindTrack(CAGEAnimation animation, ShortGuid trackId)
        {
            for (int i = 0; i < animation.floatTracks.Count; i++)
            {
                if (animation.floatTracks[i].shortGUID == trackId)
                    return animation.floatTracks[i];
            }
            return null;
        }

        #endregion
    }
}

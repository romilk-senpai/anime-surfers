using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Game.Surf.EditorTools
{
    /// <summary>
    /// Builds Miko's surf stance and slide pose as humanoid clips from muscle values, plus a controller that plays them.
    /// Regular stance: Miko faces the board's side with her left side toward the nose.
    /// </summary>
    public static class SurfPoseGenerator
    {
        private const string Folder = "Assets/Animations/Surf";
        private const string ClipPath = Folder + "/Surf_Stance.anim";
        private const string ProneClipPath = Folder + "/Surf_Prone.anim";
        private const string ControllerPath = Folder + "/MikoSurf.controller";
        private const string SlidingParameter = "Sliding";
        private const string RunningParameter = "Running";
        private const string JumpParameter = "Jump";
        private const string GroundedParameter = "Grounded";
        private const string DodgeLeftParameter = "DodgeLeft";
        private const string DodgeRightParameter = "DodgeRight";
        private const string HitLeftParameter = "HitLeft";
        private const string HitRightParameter = "HitRight";
        private const string CrashWallParameter = "CrashWall";
        private const string CrashBarrierParameter = "CrashBarrier";
        // Match RiderIK's slide and run blend times.
        private const float SlideTransition = 0.12f;
        private const float RunTransition = 0.15f;
        // Runner clips played on trains, with the board under the arm.
        private const string LocomotionFolder = "Assets/Animations/Locomotion/";
        private const string RunClipPath = LocomotionFolder + "M_katana_Blade@Run_ver_B.FBX";
        private const string RollClipPath = LocomotionFolder + "M_katana_Blade@Dodge_Front.FBX";
        private const string JumpStartClipPath = LocomotionFolder + "M_katana_Blade@Jump_Start_ZeroHeight.FBX";
        private const string JumpEndClipPath = LocomotionFolder + "M_katana_Blade@Jump_End_ZeroHeight.FBX";
        private const string DodgeLeftClipPath = LocomotionFolder + "M_katana_Blade@Run_Fast_Dodge_Left.FBX";
        private const string DodgeRightClipPath = LocomotionFolder + "M_katana_Blade@Run_Fast_Dodge_Right.FBX";
        // Same clip per side as the runner controller.
        private const string HitLeftClipPath = LocomotionFolder + "M_katana_Blade@Damage_Right_Small_ver_A.FBX";
        private const string HitRightClipPath = LocomotionFolder + "M_katana_Blade@Damage_Left_Small_ver_A.FBX";
        private const string CrashWallClipPath = LocomotionFolder + "X Bot@Standing Death Backward 01 (1).fbx";
        private const string CrashBarrierClipPath = LocomotionFolder + "M_katana_Blade@Damage_Front_High_KnockDown.FBX";
        private const string UpperTorsoMaskPath = "Assets/Animations/UpperTorsoMask.mask";
        // Timings from the runner controller, kept in Assets/Animations/Reference/MikoRunner_Reference.controller.
        // The jump starts partway in, skipping the crouch before takeoff, and holds its last frame until landing.
        private const float JumpStartOffset = 0.3302926f;
        private const float JumpStartTransition = 0.01868513f;
        private const float JumpEndExitTime = 0.3944826f;
        private const float JumpEndTransition = 0.15f;
        private const float DodgeTransition = 0.05f;
        private const float DodgeExitTime = 0.2461627f;
        private const float DodgeEndTransition = 0.25f;
        // Where each dodge picks the run cycle back up, so the feet carry on in step.
        private const float DodgeLeftRunOffset = 0.6077724f;
        private const float DodgeRightRunOffset = 0.1371844f;
        private const float HitTransition = 0.25f;
        private const float HitExitTime = 0.625f;
        private const float CrashTransition = 0.25f;
        // The wall death staggers a while before falling; sped up, she hits the water in under a second.
        private const float CrashWallSpeed = 1.6f;
        // Prone placement in Miko's space, where her left (-X) points at the nose.
        private const float ProneHeadReach = 0.55f;
        private const float ProneTorsoHeight = 0.09f;
        private const string RunnerControllerPath = "Assets/Animations/Miko.controller";
        private const string ModelPath = "Assets/Models/Miko Ooka/Miko Ooka.fbx";
        // Fingers (the katana grip) and anything not listed below come from this clip.
        private const string BasePosePath = "Assets/Animations/Locomotion/M_katana_Blade@Idle_ver_B.FBX";
        private const float LoopDuration = 2f;

        // Muscle values are not angles: legs hang straight at Front-Back ~0.6 and lower values swing them forward,
        // Front-Back on the spine leans back when positive, and Twist/Turn negative rotates toward the left (nose).
        private static readonly Dictionary<string, float> Stance = new Dictionary<string, float>
        {
            { "Spine Front-Back", -0.25f }, { "Spine Left-Right", 0f }, { "Spine Twist Left-Right", -0.3f },
            { "Chest Front-Back", -0.1f }, { "Chest Left-Right", 0f }, { "Chest Twist Left-Right", -0.2f },
            { "UpperChest Front-Back", -0.05f }, { "UpperChest Left-Right", 0f }, { "UpperChest Twist Left-Right", -0.2f },
            { "Neck Nod Down-Up", 0f }, { "Neck Tilt Left-Right", 0f }, { "Neck Turn Left-Right", -0.6f },
            { "Head Nod Down-Up", 0.1f }, { "Head Tilt Left-Right", 0f }, { "Head Turn Left-Right", -1f },

            // Wide crouch with the feet under the hips.
            { "Left Upper Leg Front-Back", -0.2f }, { "Left Upper Leg In-Out", 0.7f }, { "Left Upper Leg Twist In-Out", 0f },
            { "Left Lower Leg Stretch", 0.1f }, { "Left Lower Leg Twist In-Out", 0f },
            { "Left Foot Up-Down", -0.3f }, { "Left Foot Twist In-Out", 0f }, { "Left Toes Up-Down", 0f },
            { "Right Upper Leg Front-Back", -0.2f }, { "Right Upper Leg In-Out", 0.7f }, { "Right Upper Leg Twist In-Out", 0f },
            { "Right Lower Leg Stretch", 0.1f }, { "Right Lower Leg Twist In-Out", 0f },
            { "Right Foot Up-Down", -0.3f }, { "Right Foot Twist In-Out", 0f }, { "Right Toes Up-Down", 0f },

            // Front arm reaches toward the nose, the katana arm trails toward the tail.
            { "Left Shoulder Down-Up", 0f }, { "Left Shoulder Front-Back", 0f },
            { "Left Arm Down-Up", 0.3f }, { "Left Arm Front-Back", 0.6f }, { "Left Arm Twist In-Out", 0f },
            { "Left Forearm Stretch", 1f }, { "Left Forearm Twist In-Out", 0f },
            { "Right Shoulder Down-Up", 0f }, { "Right Shoulder Front-Back", 0f },
            { "Right Arm Down-Up", 0.2f }, { "Right Arm Front-Back", 0.6f }, { "Right Arm Twist In-Out", 0f },
            { "Right Forearm Stretch", 0.7f }, { "Right Forearm Twist In-Out", 0f }
        };

        // Added to the stance halfway through the loop: a slight dip and sway riding the swell.
        private static readonly Dictionary<string, float> Bob = new Dictionary<string, float>
        {
            { "Left Lower Leg Stretch", -0.08f }, { "Right Lower Leg Stretch", -0.08f },
            { "Left Upper Leg Front-Back", -0.04f }, { "Right Upper Leg Front-Back", -0.04f },
            { "Spine Front-Back", -0.06f }, { "Head Nod Down-Up", -0.05f },
            { "Left Arm Down-Up", -0.06f }, { "Right Arm Down-Up", 0.05f }
        };

        // Slide: belly down along the board, head raised to look ahead, hands on the rails beside the chest, heels up.
        // Spine Front-Back arches the back here, since the body is rotated to lie chest down.
        private static readonly Dictionary<string, float> Prone = new Dictionary<string, float>
        {
            { "Spine Front-Back", 0.35f }, { "Spine Left-Right", 0f }, { "Spine Twist Left-Right", 0f },
            { "Chest Front-Back", 0.3f }, { "Chest Left-Right", 0f }, { "Chest Twist Left-Right", 0f },
            { "UpperChest Front-Back", 0.2f }, { "UpperChest Left-Right", 0f }, { "UpperChest Twist Left-Right", 0f },
            { "Neck Nod Down-Up", 0.6f }, { "Neck Tilt Left-Right", 0f }, { "Neck Turn Left-Right", 0f },
            { "Head Nod Down-Up", 0.5f }, { "Head Tilt Left-Right", 0f }, { "Head Turn Left-Right", 0f },

            { "Left Upper Leg Front-Back", 0.6f }, { "Left Upper Leg In-Out", 0.15f }, { "Left Upper Leg Twist In-Out", 0f },
            { "Left Lower Leg Stretch", 0.6f }, { "Left Lower Leg Twist In-Out", 0f },
            { "Left Foot Up-Down", 0.3f }, { "Left Foot Twist In-Out", 0f }, { "Left Toes Up-Down", 0f },
            { "Right Upper Leg Front-Back", 0.6f }, { "Right Upper Leg In-Out", 0.15f }, { "Right Upper Leg Twist In-Out", 0f },
            { "Right Lower Leg Stretch", 0.6f }, { "Right Lower Leg Twist In-Out", 0f },
            { "Right Foot Up-Down", 0.3f }, { "Right Foot Twist In-Out", 0f }, { "Right Toes Up-Down", 0f },

            { "Left Shoulder Down-Up", 0f }, { "Left Shoulder Front-Back", 0f },
            { "Left Arm Down-Up", -0.3f }, { "Left Arm Front-Back", 0.5f }, { "Left Arm Twist In-Out", 0f },
            { "Left Forearm Stretch", 0.2f }, { "Left Forearm Twist In-Out", 0f },
            { "Right Shoulder Down-Up", 0f }, { "Right Shoulder Front-Back", 0f },
            { "Right Arm Down-Up", -0.3f }, { "Right Arm Front-Back", 0.5f }, { "Right Arm Twist In-Out", 0f },
            { "Right Forearm Stretch", 0.2f }, { "Right Forearm Twist In-Out", 0f }
        };

        private static readonly Regex IkGoalCurve = new Regex(@"^(Left|Right)(Foot|Hand)(T|Q)\.");

        [MenuItem("Tools/Surf/Generate Surf Pose")]
        public static void Generate()
        {
            AnimationClip basePose = AssetDatabase.LoadAllAssetsAtPath(BasePosePath).OfType<AnimationClip>()
                .First(c => !c.name.StartsWith("__preview"));
            Avatar avatar = AssetDatabase.LoadAllAssetsAtPath(ModelPath).OfType<Avatar>().First();

            Dictionary<string, float> dipped = new Dictionary<string, float>(Stance);
            foreach (KeyValuePair<string, float> offset in Bob)
            {
                dipped[offset.Key] = dipped.TryGetValue(offset.Key, out float value) ? value + offset.Value : offset.Value;
            }

            // Mecanim places the body by its center of mass, so each pose needs its own height to keep the feet
            // where the base pose has them.
            var model = (GameObject)Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath));
            float stanceHeight, dippedHeight;
            Vector3 pronePosition;
            Quaternion proneRotation;

            try
            {
                var animator = model.GetComponent<Animator>();
                if (animator == null)
                {
                    animator = model.AddComponent<Animator>();
                }

                animator.avatar = avatar;
                float footTarget = FootHeight(basePose, model, animator);
                stanceHeight = SolveBodyHeight(basePose, Stance, footTarget, model, animator);
                dippedHeight = SolveBodyHeight(basePose, dipped, footTarget, model, animator);
                SolveProne(basePose, model, animator, out pronePosition, out proneRotation);
            }
            finally
            {
                Object.DestroyImmediate(model);
            }

            AnimationClip clip = BuildClip("Surf_Stance", basePose, new[] { Stance, dipped, Stance },
                new[] { Vector3.up * stanceHeight, Vector3.up * dippedHeight, Vector3.up * stanceHeight },
                Quaternion.identity);
            SaveClip(clip, ClipPath);
            SaveClip(BuildClip("Surf_Prone", basePose, new[] { Prone }, new[] { pronePosition }, proneRotation),
                ProneClipPath);
            CreateControllerIfMissing();
            AddProneStateIfMissing();
            AddTrainStatesIfMissing();
            AddCrashStatesIfMissing();

            Debug.Log($"Surf poses generated: {ClipPath} (body height {stanceHeight:F3} / {dippedHeight:F3}), " +
                      $"{ProneClipPath}");
        }

        private static float SolveBodyHeight(AnimationClip basePose, Dictionary<string, float> pose, float footTarget,
            GameObject model, Animator animator)
        {
            // Foot height is close to linear in body height, so two samples and a secant step are enough.
            const float high = 1f;
            const float low = 0.6f;
            float footHigh = FootHeight(BuildClip("Solve", basePose, new[] { pose }, new[] { Vector3.up * high },
                Quaternion.identity), model, animator);
            float footLow = FootHeight(BuildClip("Solve", basePose, new[] { pose }, new[] { Vector3.up * low },
                Quaternion.identity), model, animator);
            return high + (footTarget - footHigh) * (low - high) / (footLow - footHigh);
        }

        /// <summary>
        /// Lays the body along Miko's left (toward the nose) with the chest down and the hips level with the chest,
        /// resting the torso on the deck with the head <see cref="ProneHeadReach"/> ahead.
        /// </summary>
        private static void SolveProne(AnimationClip basePose, GameObject model, Animator animator,
            out Vector3 position, out Quaternion rotation)
        {
            rotation = Quaternion.LookRotation(Vector3.down, Vector3.left);
            BuildClip("Solve", basePose, new[] { Prone }, new[] { Vector3.zero }, rotation).SampleAnimation(model, 0f);
            Vector3 spine = animator.GetBoneTransform(HumanBodyBones.Chest).position -
                            animator.GetBoneTransform(HumanBodyBones.Hips).position;
            rotation = Quaternion.AngleAxis(Mathf.Atan2(spine.y, -spine.x) * Mathf.Rad2Deg, Vector3.forward) * rotation;

            // Bone positions are linear in the root curves, so two samples give the exact placement.
            Vector2 origin = ProneMarks(basePose, Vector3.zero, rotation, model, animator);
            Vector2 shifted = ProneMarks(basePose, new Vector3(1f, 1f, 0f), rotation, model, animator);
            position = new Vector3((-ProneHeadReach - origin.x) / (shifted.x - origin.x),
                (ProneTorsoHeight - origin.y) / (shifted.y - origin.y), 0f);
        }

        /// <summary>Head position along the body and the height of the lowest torso bone.</summary>
        private static Vector2 ProneMarks(AnimationClip basePose, Vector3 root, Quaternion rotation, GameObject model,
            Animator animator)
        {
            BuildClip("Solve", basePose, new[] { Prone }, new[] { root }, rotation).SampleAnimation(model, 0f);
            float torso = Mathf.Min(animator.GetBoneTransform(HumanBodyBones.Hips).position.y,
                animator.GetBoneTransform(HumanBodyBones.Spine).position.y,
                animator.GetBoneTransform(HumanBodyBones.Chest).position.y);
            return new Vector2(animator.GetBoneTransform(HumanBodyBones.Head).position.x, torso) -
                   new Vector2(model.transform.position.x, model.transform.position.y);
        }

        private static float FootHeight(AnimationClip clip, GameObject model, Animator animator)
        {
            clip.SampleAnimation(model, 0f);
            float left = animator.GetBoneTransform(HumanBodyBones.LeftFoot).position.y;
            float right = animator.GetBoneTransform(HumanBodyBones.RightFoot).position.y;
            return Mathf.Min(left, right) - model.transform.position.y;
        }

        /// <summary>One key per pose, spread evenly over the loop, with the body at the given root positions.</summary>
        private static AnimationClip BuildClip(string name, AnimationClip basePose, Dictionary<string, float>[] poses,
            Vector3[] rootPositions, Quaternion rootRotation)
        {
            var clip = new AnimationClip { name = name, frameRate = 30f };
            float step = poses.Length > 1 ? LoopDuration / (poses.Length - 1) : LoopDuration;

            foreach (EditorCurveBinding binding in AnimationUtility.GetCurveBindings(basePose))
            {
                // Skip the base clip's generic and IK goal curves and its root; the root is written below.
                if (binding.type != typeof(Animator) || IkGoalCurve.IsMatch(binding.propertyName) ||
                    binding.propertyName.StartsWith("Root"))
                {
                    continue;
                }

                float baseValue = AnimationUtility.GetEditorCurve(basePose, binding).Evaluate(0f);
                var keys = new Keyframe[poses.Length > 1 ? poses.Length : 2];

                for (int i = 0; i < keys.Length; i++)
                {
                    Dictionary<string, float> pose = poses[Mathf.Min(i, poses.Length - 1)];
                    keys[i] = new Keyframe(i * step, pose.TryGetValue(binding.propertyName, out float v) ? v : baseValue);
                }

                AnimationUtility.SetEditorCurve(clip, binding, new AnimationCurve(keys));
            }

            for (int axis = 0; axis < 3; axis++)
            {
                var keys = new Keyframe[poses.Length > 1 ? poses.Length : 2];
                for (int i = 0; i < keys.Length; i++)
                {
                    keys[i] = new Keyframe(i * step, rootPositions[Mathf.Min(i, rootPositions.Length - 1)][axis]);
                }

                SetRootCurve(clip, "RootT." + "xyz"[axis], new AnimationCurve(keys));
            }

            SetRootCurve(clip, "RootQ.x", AnimationCurve.Constant(0f, LoopDuration, rootRotation.x));
            SetRootCurve(clip, "RootQ.y", AnimationCurve.Constant(0f, LoopDuration, rootRotation.y));
            SetRootCurve(clip, "RootQ.z", AnimationCurve.Constant(0f, LoopDuration, rootRotation.z));
            SetRootCurve(clip, "RootQ.w", AnimationCurve.Constant(0f, LoopDuration, rootRotation.w));

            AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = true;
            settings.loopBlendOrientation = true;
            settings.keepOriginalOrientation = true;
            settings.loopBlendPositionY = true;
            settings.keepOriginalPositionY = true;
            settings.loopBlendPositionXZ = true;
            settings.keepOriginalPositionXZ = true;
            AnimationUtility.SetAnimationClipSettings(clip, settings);
            return clip;
        }

        private static void SetRootCurve(AnimationClip clip, string property, AnimationCurve curve)
        {
            AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve("", typeof(Animator), property), curve);
        }

        private static void SaveClip(AnimationClip clip, string path)
        {
            if (!AssetDatabase.IsValidFolder(Folder))
            {
                AssetDatabase.CreateFolder("Assets/Animations", "Surf");
            }

            var existing = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            if (existing != null)
            {
                // Overwrite in place so the controller keeps its reference.
                EditorUtility.CopySerialized(clip, existing);
                EditorUtility.SetDirty(existing);
            }
            else
            {
                AssetDatabase.CreateAsset(clip, path);
            }

            AssetDatabase.SaveAssets();
        }

        /// <summary>Adds the slide pose to the controller, entered and left on the Sliding parameter.</summary>
        private static void AddProneStateIfMissing()
        {
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            AnimatorStateMachine machine = controller.layers[0].stateMachine;
            if (machine.states.Any(s => s.state.name == "Prone"))
            {
                return;
            }

            if (controller.parameters.All(p => p.name != SlidingParameter))
            {
                controller.AddParameter(SlidingParameter, AnimatorControllerParameterType.Bool);
            }

            AnimatorState prone = machine.AddState("Prone");
            prone.motion = AssetDatabase.LoadAssetAtPath<AnimationClip>(ProneClipPath);
            prone.iKOnFeet = false;
            AddSlideTransition(machine.defaultState, prone, AnimatorConditionMode.If);
            AddSlideTransition(prone, machine.defaultState, AnimatorConditionMode.IfNot);
            AssetDatabase.SaveAssets();
        }

        private static void AddSlideTransition(AnimatorState from, AnimatorState to, AnimatorConditionMode mode)
        {
            AddTransition(from, to, mode, SlidingParameter, SlideTransition);
        }

        /// <summary>
        /// Adds the runner moves played on trains: run, roll while sliding, jump, and dodges on lane changes. Losing
        /// Running anywhere among them returns to the surf stance, so a jump that ends over the water lands on the board.
        /// </summary>
        private static void AddTrainStatesIfMissing()
        {
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            AnimatorStateMachine machine = controller.layers[0].stateMachine;
            if (machine.states.Any(s => s.state.name == "Run"))
            {
                return;
            }

            AddParameterIfMissing(controller, RunningParameter, AnimatorControllerParameterType.Bool);
            AddParameterIfMissing(controller, GroundedParameter, AnimatorControllerParameterType.Bool);
            AddParameterIfMissing(controller, JumpParameter, AnimatorControllerParameterType.Trigger);
            AddParameterIfMissing(controller, DodgeLeftParameter, AnimatorControllerParameterType.Trigger);
            AddParameterIfMissing(controller, DodgeRightParameter, AnimatorControllerParameterType.Trigger);

            AnimatorState surf = machine.defaultState;
            AnimatorState prone = machine.states.First(s => s.state.name == "Prone").state;
            AnimatorState run = AddClipState(machine, "Run", RunClipPath);
            AnimatorState roll = AddClipState(machine, "Roll", RollClipPath);
            AnimatorState jumpStart = AddClipState(machine, "JumpStart", JumpStartClipPath);
            AnimatorState jumpEnd = AddClipState(machine, "JumpEnd", JumpEndClipPath);
            AnimatorState dodgeLeft = AddClipState(machine, "DodgeLeft", DodgeLeftClipPath);
            AnimatorState dodgeRight = AddClipState(machine, "DodgeRight", DodgeRightClipPath);

            // The roll plays over one slide.
            BoardSettings settings = AssetDatabase.FindAssets("t:" + nameof(BoardSettings))
                .Select(guid => AssetDatabase.LoadAssetAtPath<BoardSettings>(AssetDatabase.GUIDToAssetPath(guid)))
                .FirstOrDefault();
            if (settings != null)
            {
                roll.speed = roll.motion.averageDuration / settings.SlideDuration;
            }

            // On trains the slide is the roll, not lying down on the board.
            foreach (AnimatorStateTransition transition in surf.transitions.Where(t =>
                         t.destinationState == prone && t.conditions.All(c => c.parameter != RunningParameter)))
            {
                transition.AddCondition(AnimatorConditionMode.IfNot, 0f, RunningParameter);
            }

            AddTransition(prone, roll, AnimatorConditionMode.If, RunningParameter, RunTransition);
            MoveTransitionFirst(prone);
            AddTransition(surf, run, AnimatorConditionMode.If, RunningParameter, RunTransition);

            AddJumpTransition(run, jumpStart);
            AddSlideTransition(run, roll, AnimatorConditionMode.If);
            AddDodge(run, dodgeLeft, DodgeLeftParameter, DodgeLeftRunOffset);
            AddDodge(run, dodgeRight, DodgeRightParameter, DodgeRightRunOffset);

            // Rolling off the train keeps sliding on the board.
            AnimatorStateTransition rollToProne =
                AddTransition(roll, prone, AnimatorConditionMode.IfNot, RunningParameter, RunTransition);
            rollToProne.AddCondition(AnimatorConditionMode.If, 0f, SlidingParameter);
            AddJumpTransition(roll, jumpStart);
            AddSlideTransition(roll, run, AnimatorConditionMode.IfNot);

            foreach (AnimatorState state in new[] { jumpStart, jumpEnd, dodgeLeft, dodgeRight })
            {
                AddSlideTransition(state, roll, AnimatorConditionMode.If);
            }

            foreach (AnimatorState state in new[] { jumpEnd, dodgeLeft, dodgeRight })
            {
                AddJumpTransition(state, jumpStart);
            }

            AnimatorStateTransition startToEnd =
                AddTransition(jumpStart, jumpEnd, AnimatorConditionMode.If, GroundedParameter, 0f);
            startToEnd.hasFixedDuration = false;
            AddExitTransition(jumpEnd, run, JumpEndExitTime, JumpEndTransition, 0f);

            // Checked before the others.
            foreach (AnimatorState state in new[] { run, roll, jumpStart, jumpEnd, dodgeLeft, dodgeRight })
            {
                AddTransition(state, surf, AnimatorConditionMode.IfNot, RunningParameter, RunTransition);
                MoveTransitionFirst(state);
            }

            // Ahead of the stance fallback, so rolling off the train goes straight to lying on the board.
            MoveTransitionFirst(roll, rollToProne);
            AssetDatabase.SaveAssets();
        }

        /// <summary>
        /// Adds the crash reactions: falling backward from a wall, knocked down by a barrier, closing the eyes, and
        /// flinching on a layer of its own when bouncing off an obstacle's side, as in the runner controller.
        /// </summary>
        [MenuItem("Tools/Surf/Add Crash States")]
        public static void AddCrashStatesIfMissing()
        {
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            AnimatorStateMachine machine = controller.layers[0].stateMachine;
            if (machine.states.Any(s => s.state.name == "CrashWall"))
            {
                return;
            }

            AddParameterIfMissing(controller, CrashWallParameter, AnimatorControllerParameterType.Trigger);
            AddParameterIfMissing(controller, CrashBarrierParameter, AnimatorControllerParameterType.Trigger);
            AddParameterIfMissing(controller, HitLeftParameter, AnimatorControllerParameterType.Trigger);
            AddParameterIfMissing(controller, HitRightParameter, AnimatorControllerParameterType.Trigger);

            // RiderIK applies the root motion that carries her off the board.
            AnimatorState crashWall = AddClipState(machine, "CrashWall", CrashWallClipPath);
            crashWall.speed = CrashWallSpeed;
            AddCrash(machine, crashWall, CrashWallParameter);
            AddCrash(machine, AddClipState(machine, "CrashBarrier", CrashBarrierClipPath), CrashBarrierParameter);

            AnimatorControllerLayer facial = controller.layers.FirstOrDefault(l => l.name == "Facial");
            var eyesClose = AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/Animations/MikoBlendShapes/EyesClose.anim");
            if (facial != null && eyesClose != null)
            {
                AnimatorState closed = facial.stateMachine.AddState("EyesClose");
                closed.motion = eyesClose;
                AddCrash(facial.stateMachine, closed, CrashWallParameter);
                AddCrash(facial.stateMachine, closed, CrashBarrierParameter);
            }

            // Additive, so the flinch shows over the surf stance as well as the run.
            var hitMachine = new AnimatorStateMachine { name = "Hit", hideFlags = HideFlags.HideInHierarchy };
            AssetDatabase.AddObjectToAsset(hitMachine, controller);
            AnimatorState empty = hitMachine.AddState("Empty");
            hitMachine.defaultState = empty;
            AddHit(hitMachine, empty, "HitLeft", HitLeftClipPath, HitLeftParameter);
            AddHit(hitMachine, empty, "HitRight", HitRightClipPath, HitRightParameter);

            controller.AddLayer(new AnimatorControllerLayer
            {
                name = "Hit",
                avatarMask = AssetDatabase.LoadAssetAtPath<AvatarMask>(UpperTorsoMaskPath),
                defaultWeight = 1f,
                blendingMode = AnimatorLayerBlendingMode.Additive,
                stateMachine = hitMachine
            });

            AssetDatabase.SaveAssets();
        }

        private static void AddCrash(AnimatorStateMachine machine, AnimatorState state, string parameter)
        {
            AnimatorStateTransition transition = machine.AddAnyStateTransition(state);
            transition.hasExitTime = false;
            transition.duration = CrashTransition;
            transition.canTransitionToSelf = false;
            transition.AddCondition(AnimatorConditionMode.If, 0f, parameter);
        }

        private static void AddHit(AnimatorStateMachine machine, AnimatorState empty, string name, string clipPath,
            string parameter)
        {
            AnimatorState hit = AddClipState(machine, name, clipPath);
            AnimatorStateTransition transition = machine.AddAnyStateTransition(hit);
            transition.hasExitTime = false;
            transition.duration = HitTransition;
            transition.AddCondition(AnimatorConditionMode.If, 0f, parameter);

            AnimatorStateTransition exit = hit.AddTransition(empty);
            exit.hasExitTime = true;
            exit.exitTime = HitExitTime;
            exit.duration = HitTransition;
        }

        private static void AddParameterIfMissing(AnimatorController controller, string name,
            AnimatorControllerParameterType type)
        {
            if (controller.parameters.All(p => p.name != name))
            {
                controller.AddParameter(name, type);
            }
        }

        private static void AddJumpTransition(AnimatorState from, AnimatorState jumpStart)
        {
            AnimatorStateTransition transition =
                AddTransition(from, jumpStart, AnimatorConditionMode.If, JumpParameter, JumpStartTransition);
            transition.offset = JumpStartOffset;
        }

        /// <summary>Plays the dodge on its trigger while grounded, then rejoins the run at the given cycle phase.</summary>
        private static void AddDodge(AnimatorState run, AnimatorState dodge, string parameter, float runOffset)
        {
            AnimatorStateTransition transition =
                AddTransition(run, dodge, AnimatorConditionMode.If, GroundedParameter, DodgeTransition);
            transition.AddCondition(AnimatorConditionMode.If, 0f, parameter);
            AddExitTransition(dodge, run, DodgeExitTime, DodgeEndTransition, runOffset);
        }

        /// <summary>Leaves at the exit time; the destination's own transitions may cut the blend short.</summary>
        private static void AddExitTransition(AnimatorState from, AnimatorState to, float exitTime, float duration,
            float offset)
        {
            AnimatorStateTransition transition = from.AddTransition(to);
            transition.hasExitTime = true;
            transition.exitTime = exitTime;
            transition.duration = duration;
            transition.offset = offset;
            transition.interruptionSource = TransitionInterruptionSource.Destination;
        }

        private static AnimatorState AddClipState(AnimatorStateMachine machine, string name, string clipPath)
        {
            AnimatorState state = machine.AddState(name);
            state.motion = AssetDatabase.LoadAllAssetsAtPath(clipPath).OfType<AnimationClip>()
                .First(c => !c.name.StartsWith("__preview"));
            return state;
        }

        /// <summary>Moves the given transition, or the last one added, to the front of the state's list.</summary>
        private static void MoveTransitionFirst(AnimatorState state, AnimatorStateTransition transition = null)
        {
            List<AnimatorStateTransition> transitions = state.transitions.ToList();
            transition = transition ?? transitions[transitions.Count - 1];
            transitions.Remove(transition);
            transitions.Insert(0, transition);
            state.transitions = transitions.ToArray();
        }

        private static AnimatorStateTransition AddTransition(AnimatorState from, AnimatorState to,
            AnimatorConditionMode mode, string parameter, float duration)
        {
            AnimatorStateTransition transition = from.AddTransition(to);
            transition.hasExitTime = false;
            transition.duration = duration;
            transition.AddCondition(mode, 0f, parameter);
            return transition;
        }

        private static void CreateControllerIfMissing()
        {
            if (AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath) != null)
            {
                return;
            }

            AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
            // RiderIK pins the feet and drives the body from OnAnimatorIK.
            AnimatorControllerLayer[] layers = controller.layers;
            layers[0].iKPass = true;
            controller.layers = layers;
            AnimatorStateMachine baseMachine = controller.layers[0].stateMachine;
            AnimatorState surf = baseMachine.AddState("Surf");
            surf.motion = AssetDatabase.LoadAssetAtPath<AnimationClip>(ClipPath);
            // The generated clip has no IK goal curves.
            surf.iKOnFeet = false;
            baseMachine.defaultState = surf;

            // Keep blinking from the runner controller.
            var runner = AssetDatabase.LoadAssetAtPath<AnimatorController>(RunnerControllerPath);
            AnimatorControllerLayer runnerFacial = runner.layers.FirstOrDefault(l => l.name == "Facial");
            ChildAnimatorState blink = runnerFacial?.stateMachine.states.FirstOrDefault(s => s.state.name == "Blink") ?? default;

            if (blink.state != null)
            {
                var machine = new AnimatorStateMachine { name = "Facial", hideFlags = HideFlags.HideInHierarchy };
                AssetDatabase.AddObjectToAsset(machine, controller);
                AnimatorState state = machine.AddState("Blink");
                state.motion = blink.state.motion;
                machine.defaultState = state;

                controller.AddLayer(new AnimatorControllerLayer
                {
                    name = "Facial",
                    avatarMask = runnerFacial.avatarMask,
                    defaultWeight = runnerFacial.defaultWeight,
                    blendingMode = runnerFacial.blendingMode,
                    stateMachine = machine
                });
            }

            AssetDatabase.SaveAssets();
        }
    }
}

using TrashCats.Domain;
using UnityEngine;

namespace TrashCats.Presentation.Hideout
{
    /// <summary>
    /// Where things stand in the hideout, in the 0..1 space of the background picture bg_hideout (x to the right, y up;
    /// 1024x1536, Docs/ArtDirection/Generated/raw_v7/bg_hideout.png): the back wall's base at y ≈ 0.425 with the door at
    /// x 0.36..0.62 and the street gap at the right edge; the yard below. Props are rects; cats stand on foot
    /// points with the clip they play and the side they face; cats on a prop draw over it. The can hangs right of the
    /// door, the dumpster stands at the back by the right wall, the bench mat lies in the front left corner.
    /// </summary>
    public static class HideoutLayout
    {
        public const float PictureAspect = 1024f / 1536f;

        /// <summary>Crop the sky first, keep the ground.</summary>
        public static readonly Vector2 Focus = new Vector2(0.5f, 0.2f);

        /// <summary>Picture height per sprite pixel at the front edge: a standing cat (~400 px) is about a third of the picture.</summary>
        public const float HeightPerPixel = 0.25f / 312f;

        /// <summary>Things get smaller towards the wall (y 0.425).</summary>
        public static float DepthScale(float y) => Mathf.Lerp(1f, 0.72f, Mathf.InverseLerp(0.05f, 0.45f, y));

        public enum CatClip
        {
            Sit,
            Sleep,
            Walk,
            Bench,
            Punch,
            Rope,
        }

        /// <summary>How a cat appears at a spot.</summary>
        public readonly struct CatSpot
        {
            public Vector2 Foot { get; }
            public CatClip Clip { get; }
            public bool FaceLeft { get; }
            /// <summary>Drawn over the station prop it lies on (sleep on the boxes, bench press on the mat).</summary>
            public bool OverProp { get; }
            public float Scale { get; }
            public bool Hidden { get; }
            /// <summary>Box height (share of the picture height) when it is fixed, else 0 (from the sprite and depth).</summary>
            public float Height { get; }
            /// <summary>Draw order: lower draws on top; NaN = by the foot point.</summary>
            public float Depth { get; }

            public CatSpot(Vector2 foot, CatClip clip, bool faceLeft = false, bool overProp = false, float scale = 1f, bool hidden = false, float height = 0f,
                float depth = float.NaN)
            {
                Height = height;
                Foot = foot;
                Clip = clip;
                FaceLeft = faceLeft;
                OverProp = overProp;
                Scale = scale;
                Hidden = hidden;
                Depth = depth;
            }
        }

        /// <summary>
        /// A prop as placed in the layout scene (Scenes/Dev/HideoutLayout.unity, see HideoutLayoutSceneBuilder): sprite,
        /// bottom-centre point, size (shares of the picture), rotation about that point (degrees, counter-clockwise),
        /// mirrored or not, and draw order (lower draws on top).
        /// </summary>
        public readonly struct PropPlace
        {
            public string Sprite { get; }
            public Vector2 Foot { get; }
            public Vector2 Size { get; }
            public float Rotation { get; }
            public bool Flip { get; }
            public float Depth { get; }
            public Vector2 Min => Foot - new Vector2(Size.x * 0.5f, 0f);
            public Vector2 Max => Foot + new Vector2(Size.x * 0.5f, Size.y);

            public PropPlace(string sprite, Vector2 foot, Vector2 size, float rotation = 0f, bool flip = false, float depth = float.NaN)
            {
                Sprite = sprite;
                Foot = foot;
                Size = size;
                Rotation = rotation;
                Flip = flip;
                Depth = float.IsNaN(depth) ? foot.y : depth;
            }
        }

        // placed by hand in the layout scene (export 2026-10-14, Docs/ArtDirection/hideout_layout_export.json)

        /// <summary>The bench press mat with brick stands, its pillow end at the wall on the left.</summary>
        public static readonly PropPlace Bench = new PropPlace("prop_bench", new Vector2(0.2143f, 0.2404f), new Vector2(0.42f, 0.1963f), -2.86f, true, 0.31f);

        /// <summary>The dumpster at the back on the right; a cat rummaging in it is not shown, only its badge above.</summary>
        public static readonly PropPlace Dumpster = new PropPlace("prop_upg_dumpster_v1_angled", new Vector2(0.9538f, 0.4165f), new Vector2(0.2774f, 0.1742f), -1.41f, true, 0.425f);

        /// <summary>The dumpster's nudge in canvas units, right and up (set by hand in the editor, 2026-10-15).</summary>
        public static readonly Vector2 DumpsterShift = new Vector2(75.1f, 15.5f);

        /// <summary>The box bed, sized for Ponchik (the biggest sleeper).</summary>
        public static readonly PropPlace Bed = new PropPlace("prop_upg_boxes", new Vector2(0.915f, 0.0117f), new Vector2(0.52f, 0.3055f), 0f, true, -0.02f);

        /// <summary>The can on its rope, right of the door; it draws over the dumpster.</summary>
        public static readonly PropPlace Can = new PropPlace("prop_upg_can", new Vector2(0.7375f, 0.4954f), new Vector2(0.0979f, 0.1182f), depth: 0.424f);

        /// <summary>The wrought-iron bracket holding the can: wall plate on the left, hook over the can.</summary>
        public static readonly PropPlace Bracket = new PropPlace("prop_bracket", new Vector2(0.6904f, 0.6094f), new Vector2(0.12f, 0.0623f), depth: 0.4241f);

        /// <summary>The skipping rope on a nail by the door.</summary>
        public static readonly PropPlace RopeProp = new PropPlace("prop_upg_mouse", new Vector2(0.3097f, 0.5206f), new Vector2(0.065f, 0.1059f), depth: 0.497f);

        /// <summary>
        /// The barbell laid with the ends of its bar on the mat's two brick stands, the cans hanging outside them; in 0..1
        /// of the mat picture as drawn (before the mirroring): where the bar rests (middle between the stands' tops), size,
        /// tilt (the stands stand at different heights) and the bar's underside as a share of the sprite height from its
        /// bottom (the pivot). Hidden while a cat presses it.
        /// </summary>
        public static readonly (string Sprite, Vector2 Seat, Vector2 Size, float Rotation, float BarUnderside) Barbell =
            ("prop_barbell", new Vector2(0.667f, 0.905f), new Vector2(0.98f, 0.291f), -7.3f, 0.49f);

        /// <summary>Where the barbell's plate hangs: on the wall over the barbell, in 0..1 of the mat's box (top of the plate).</summary>
        public static readonly Vector2 BenchPlate = new Vector2(0.27f, 1.38f);

        /// <summary>The way out to the street: an EXIT signpost in the empty front left corner.</summary>
        /// Nearer to the camera than the cat on the skipping rope: a bit bigger, lower, and drawn over it.
        public static readonly PropPlace ExitSign = new PropPlace("prop_exit_sign", new Vector2(0.115f, 0.025f), new Vector2(0.2283f, 0.22f), depth: 0.0f);

        /// <summary>
        /// Top of the head in the first frame of each cat's idle clip (as drawn, facing right; 0..1 of the frame), where
        /// the speech bubble's tail points.
        /// </summary>
        public static Vector2 IdleHead(string viewKey) => viewKey switch
        {
            "ryzhy" => new Vector2(0.66f, 0.95f),
            "shnyr" => new Vector2(0.43f, 0.96f),
            "ponchik" => new Vector2(0.49f, 0.96f),
            _ => new Vector2(0.5f, 0.95f),
        };

        public static PropPlace Prop(ActivityDefinition activity) => activity.Kind switch
        {
            ActivityKind.Scavenge => ExitSign,
            ActivityKind.Rummage => Dumpster,
            ActivityKind.Sleep => Bed,
            _ => activity.TrainedAttribute switch
            {
                // the can trains technique, the skipping rope endurance, the barbell strength
                AttributeType.Technique => Can,
                AttributeType.Endurance => RopeProp,
                _ => Bench,
            },
        };

        /// <summary>Sprite key of the object an upgrade improves (for the upgrade window).</summary>
        public static string UpgradeSprite(UpgradeId id) => id.Value switch
        {
            "upg_boxes" => Bed.Sprite,
            "upg_dumpster" => Dumpster.Sprite,
            "upg_can" => Can.Sprite,
            "upg_mouse" => RopeProp.Sprite,
            "upg_wheel" => Bench.Sprite,
            _ => null,
        };

        /// <summary>Prop rect (min, max) for a station.</summary>
        public static (Vector2 Min, Vector2 Max) Station(ActivityDefinition activity) => (Prop(activity).Min, Prop(activity).Max);

        /// <summary>Where the cat doing <paramref name="activity"/> stands; <paramref name="index"/> spreads several cats.</summary>
        public static CatSpot Spot(ActivityDefinition activity, int index, string viewKey)
        {
            var shift = new Vector2(-0.09f * index, -0.03f * index);
            return activity.Kind switch
            {
                ActivityKind.Scavenge => new CatSpot(ExitSign.Foot, CatClip.Walk, hidden: true),
                ActivityKind.Rummage => new CatSpot(new Vector2(Dumpster.Foot.x, Dumpster.Max.y), CatClip.Walk, hidden: true),
                // lying on the blanket, drawn over the bed
                ActivityKind.Sleep => new CatSpot(new Vector2(0.8438f, 0.2135f) + shift * 0.5f, CatClip.Sleep, overProp: true, scale: 1.147f, depth: -0.022f),
                _ => activity.TrainedAttribute switch
                {
                    // right of the can at arm's length, hitting to the left
                    AttributeType.Technique => new CatSpot(new Vector2(0.9521f, 0.3893f) + shift, CatClip.Punch, faceLeft: true, scale: 0.799f, depth: 0.37f + shift.y),
                    AttributeType.Endurance => new CatSpot(new Vector2(0.3115f, 0.0703f) + shift, CatClip.Rope, scale: 1.0146f, depth: 0.029f + shift.y),
                    // mirrored with the mat: the brick stands at the right end
                    _ => new CatSpot(Vector2.zero, CatClip.Bench, faceLeft: true, overProp: true, depth: 0.188f),
                },
            };
        }

        /// <summary>
        /// A cat pressing on the mat, relative to the mat's unrotated rect: bottom centre of the frame box (0..1 of the
        /// rect) and the box height as a share of the rect height. Ponchik as placed in the layout scene; Ryzhy and Shnyr
        /// keep their offsets to him from the approved previews (Tools/compose_bench_preview.py). The fit was made for
        /// frames of Canvas pixels high; taller frames (a drawn barbell sticks out above) keep the cat's size and feet.
        /// </summary>
        public static (Vector2 Foot, float Height, float Canvas)? BenchFit(string viewKey) => viewKey switch
        {
            "ryzhy" => (new Vector2(0.56f, 0.06f), 1.506f, 364f / 0.78f), // drawn barbell frames (2026-10-16): a longer body, smaller
            "shnyr" => (new Vector2(0.6257f, 0.0886f), 1.31f, 372f),
            "ponchik" => (new Vector2(0.62f, 0.07f), 1.494f, 361f / 0.8f),
            _ => null,
        };

        /// <summary>
        /// Idle cats in a row in the middle of the yard: Ponchik in the centre facing the player, Ryzhy on the right
        /// looking right, Shnyr on the left looking left (as placed in the layout scene); others fill in behind.
        /// </summary>
        public static CatSpot Idle(string viewKey, int index, bool alone = false) => viewKey switch
        {
            // alone in the gang (the start of the game): in the middle, on Ponchik's spot, a little smaller as it is farther
            "ryzhy" when alone => new CatSpot(new Vector2(0.5859f, 0.2473f), CatClip.Sit, height: 0.2761f * DepthScale(0.2473f) / DepthScale(0.2223f), depth: 0.299f),
            // a gang shot: Ponchik a step behind in the middle, Shnyr and Ryzhy in front, overlapping him at the shoulders
            "ponchik" => new CatSpot(new Vector2(0.5859f, 0.2473f), CatClip.Sit, height: 0.2876f, depth: 0.299f),
            "ryzhy" => new CatSpot(new Vector2(0.6514f, 0.2223f), CatClip.Sit, height: 0.2761f, depth: 0.274f),
            "shnyr" => new CatSpot(new Vector2(0.4717f, 0.2223f), CatClip.Sit, faceLeft: true, height: 0.2721f, depth: 0.274f),
            _ => new CatSpot(new Vector2(0.4f + 0.1f * index, 0.28f), CatClip.Sit),
        };
    }
}

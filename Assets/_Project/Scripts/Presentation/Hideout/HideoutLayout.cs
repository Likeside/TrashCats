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
            /// <summary>Box height (share of the picture height) when it is fixed by a prop, else 0 (from the sprite and depth).</summary>
            public float Height { get; }

            public CatSpot(Vector2 foot, CatClip clip, bool faceLeft = false, bool overProp = false, float scale = 1f, bool hidden = false, float height = 0f)
            {
                Height = height;
                Foot = foot;
                Clip = clip;
                FaceLeft = faceLeft;
                OverProp = overProp;
                Scale = scale;
                Hidden = hidden;
            }
        }

        /// <summary>
        /// The bench press mat with brick stands, in the left corner with its pillow end at the wall; the rect keeps the
        /// mat sprite's aspect (448/639). Its far half lies behind the cats standing in front of it: it sorts by
        /// <see cref="BenchDepth"/>, not by its bottom edge.
        /// </summary>
        public static readonly (Vector2 Min, Vector2 Max) Bench = (new Vector2(0.01f, 0.19f), new Vector2(0.43f, 0.19f + 0.42f * 448f / 639f * PictureAspect));

        public static float BenchDepth => Bench.Min.y + 0.12f;

        /// <summary>
        /// The dumpster at the back on the right, its long side against the wall and its bottom on the wall's base line;
        /// the rect keeps the sprite's aspect (744/1167). A cat rummaging in it is not shown, only its badge above.
        /// </summary>
        public static readonly (Vector2 Min, Vector2 Max) Dumpster = (new Vector2(0.79f, 0.425f), new Vector2(1.03f, 0.425f + 0.24f * 744f / 1167f * PictureAspect));

        /// <summary>The box bed, sized for Ponchik (the biggest sleeper); the middle of its blanket is at 74% of its height.</summary>
        public static readonly (Vector2 Min, Vector2 Max) Bed = (new Vector2(0.55f, -0.02f), new Vector2(1.07f, 0.286f));

        /// <summary>Prop rect (min, max) for a station.</summary>
        public static (Vector2 Min, Vector2 Max) Station(ActivityDefinition activity) => activity.Kind switch
        {
            ActivityKind.Scavenge => (new Vector2(0.806f, 0.722f), new Vector2(0.95f, 0.83f)), // the street gap on the right
            ActivityKind.Rummage => Dumpster,
            ActivityKind.Sleep => Bed,
            _ => activity.TrainedAttribute switch
            {
                // can on a bracket on the wall right of the door, its body at the fist height of the punching cat
                AttributeType.Strength => (new Vector2(0.6975f, 0.491f), new Vector2(0.8325f, 0.654f)),
                AttributeType.Agility => (new Vector2(0.35f, 0.497f), new Vector2(0.415f, 0.65f)),  // skipping rope on a nail by the door
                _ => Bench,
            },
        };

        /// <summary>Where the cat doing <paramref name="activity"/> stands; <paramref name="index"/> spreads several cats.</summary>
        public static CatSpot Spot(ActivityDefinition activity, int index, string viewKey)
        {
            var shift = new Vector2(-0.09f * index, -0.03f * index);
            var bedTop = new Vector2(Bed.Min.x + (Bed.Max.x - Bed.Min.x) * 0.66f, Bed.Min.y + (Bed.Max.y - Bed.Min.y) * 0.74f);
            return activity.Kind switch
            {
                ActivityKind.Scavenge => new CatSpot(new Vector2(0.887f, 0.722f), CatClip.Walk, hidden: true),
                ActivityKind.Rummage => new CatSpot(new Vector2((Dumpster.Min.x + Dumpster.Max.x) * 0.5f, Dumpster.Max.y), CatClip.Walk, hidden: true),
                // lying on the blanket at full size (no depth shrink), drawn over the bed
                ActivityKind.Sleep => new CatSpot(bedTop + shift * 0.5f, CatClip.Sleep, overProp: true, scale: 0.85f / DepthScale(bedTop.y)),
                _ => activity.TrainedAttribute switch
                {
                    // left of the can at arm's length, hitting to the right
                    AttributeType.Strength => new CatSpot(new Vector2(0.525f, 0.371f) + new Vector2(-shift.x, shift.y), CatClip.Punch),
                    AttributeType.Agility => new CatSpot(new Vector2(0.49f, 0.03f) + shift, CatClip.Rope),
                    // mirrored with the mat: the brick stands at the right end
                    _ => new CatSpot(Vector2.zero, CatClip.Bench, faceLeft: true, overProp: true),
                },
            };
        }

        /// <summary>
        /// A cat pressing on the mat, relative to the mat's rect: bottom centre of the frame box (0..1 of the rect) and
        /// the box height as a share of the rect height. Taken from the approved previews
        /// (Tools/compose_bench_preview.py: mat x0.729, Ryzhy and Ponchik x1.35 at -90,-195, Shnyr x1.15 at -67,-136),
        /// mirrored left to right with the mat (x = 1 - preview x).
        /// </summary>
        public static (Vector2 Foot, float Height)? BenchFit(string viewKey) => viewKey switch
        {
            "ryzhy" => (new Vector2(0.555f, 0.092f), 1.506f),
            "shnyr" => (new Vector2(0.556f, 0.107f), 1.31f),
            "ponchik" => (new Vector2(0.571f, 0.104f), 1.494f),
            _ => null,
        };

        /// <summary>
        /// Idle cats in a row in the middle of the yard: Ponchik in the centre facing the player, Ryzhy on the right
        /// looking right, Shnyr on the left looking left; others fill in behind.
        /// </summary>
        public static CatSpot Idle(string viewKey, int index) => viewKey switch
        {
            // a gang shot: Ponchik a step behind in the middle, Shnyr and Ryzhy in front, overlapping him at the shoulders
            "ponchik" => new CatSpot(new Vector2(0.48f, 0.3f), CatClip.Sit),
            "ryzhy" => new CatSpot(new Vector2(0.62f, 0.275f), CatClip.Sit),
            "shnyr" => new CatSpot(new Vector2(0.355f, 0.275f), CatClip.Sit, faceLeft: true),
            _ => new CatSpot(new Vector2(0.4f + 0.1f * index, 0.28f), CatClip.Sit),
        };
    }
}

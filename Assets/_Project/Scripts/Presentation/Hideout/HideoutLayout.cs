using TrashCats.Domain;
using UnityEngine;

namespace TrashCats.Presentation.Hideout
{
    /// <summary>
    /// Where things stand in the hideout, in the 0..1 space of the background picture bg_hideout (x to the right, y up;
    /// 1024x1536, Docs/ArtDirection/Generated/raw_v7/bg_hideout.png): the back wall's base at y ≈ 0.425 with the door at
    /// x 0.36..0.62 and the street gap at the right edge; the yard below. Props are rects; cats stand on foot
    /// points with the clip they play and the side they face; cats in or on a prop draw over it.
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
            Run,
            Punch,
            Rope,
            Dig,
        }

        /// <summary>How a cat appears at a spot.</summary>
        public readonly struct CatSpot
        {
            public Vector2 Foot { get; }
            public CatClip Clip { get; }
            public bool FaceLeft { get; }
            /// <summary>Drawn over the station prop it stands on / runs in (sleep on the boxes, run in the wheel).</summary>
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

        /// <summary>The wheel, standing on the yard by the left wall.</summary>
        public static readonly (Vector2 Min, Vector2 Max) Wheel = (new Vector2(0.53f, 0.34f), new Vector2(1.06f, 0.735f));

        /// <summary>The box bed, sized for Ponchik (the biggest sleeper); the middle of its blanket is at 74% of its height.</summary>
        public static readonly (Vector2 Min, Vector2 Max) Bed = (new Vector2(0.55f, -0.02f), new Vector2(1.07f, 0.286f));

        /// <summary>Prop rect (min, max) for a station.</summary>
        public static (Vector2 Min, Vector2 Max) Station(ActivityDefinition activity) => activity.Kind switch
        {
            ActivityKind.Scavenge => (new Vector2(0.806f, 0.722f), new Vector2(0.95f, 0.83f)), // the street gap on the right
            ActivityKind.Rummage => (new Vector2(-0.17f, 0.0f), new Vector2(0.26f, 0.265f)),    // big dumpster, its edge in front
            ActivityKind.Sleep => Bed,
            _ => activity.TrainedAttribute switch
            {
                // can on a bracket on the wall, its body at the fist height of the punching cat
                AttributeType.Strength => (new Vector2(0.122f, 0.491f), new Vector2(0.257f, 0.654f)),
                AttributeType.Agility => (new Vector2(0.35f, 0.497f), new Vector2(0.415f, 0.65f)),  // skipping rope on a nail by the door
                _ => Wheel,
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
                // head first in the dumpster: the cut line of the clip hides under the bin's front rim; Ponchik's clip
                // is drawn the other way round, so it is mirrored
                ActivityKind.Rummage => viewKey == "ponchik"
                    ? new CatSpot(new Vector2(0.11f + 0.05f * index, 0.178f), CatClip.Dig, faceLeft: true)
                    : new CatSpot(new Vector2(0.135f + 0.05f * index, 0.178f), CatClip.Dig),
                // lying on the blanket at full size (no depth shrink), drawn over the bed
                ActivityKind.Sleep => new CatSpot(bedTop + shift * 0.5f, CatClip.Sleep, overProp: true, scale: 0.85f / DepthScale(bedTop.y)),
                _ => activity.TrainedAttribute switch
                {
                    // right of the can at arm's length, hitting to the left
                    AttributeType.Strength => new CatSpot(new Vector2(0.43f, 0.371f) + shift, CatClip.Punch, faceLeft: true),
                    AttributeType.Agility => new CatSpot(new Vector2(0.4f, 0.03f) + shift, CatClip.Rope),
                    _ => new CatSpot(Vector2.zero, CatClip.Run, faceLeft: true, overProp: true),
                },
            };
        }

        /// <summary>
        /// A cat running in the wheel, relative to the wheel's rect: foot point (0..1 of the rect) and the cat box height
        /// as a share of the rect height. Solved against the sprites (the drum's inner floor of wheel frame 0 and the
        /// union of the cat's run frames): every pixel stays inside the drum, the feet on its floor.
        /// </summary>
        public static (Vector2 Foot, float Height)? RunFit(string viewKey) => viewKey switch
        {
            "ryzhy" => (new Vector2(0.51f, 0.199f), 0.568f),
            "shnyr" => (new Vector2(0.537f, 0.191f), 0.618f),
            "ponchik" => (new Vector2(0.559f, 0.191f), 0.565f),
            _ => null,
        };

        /// <summary>
        /// Idle cats in a row in the middle of the yard: Ponchik in the centre facing the player, Ryzhy on the right
        /// looking right, Shnyr on the left looking left; others fill in behind.
        /// </summary>
        public static CatSpot Idle(string viewKey, int index) => viewKey switch
        {
            // a gang shot: Ponchik a step behind in the middle, Shnyr and Ryzhy in front, overlapping him at the shoulders
            "ponchik" => new CatSpot(new Vector2(0.45f, 0.33f), CatClip.Sit),
            "ryzhy" => new CatSpot(new Vector2(0.59f, 0.275f), CatClip.Sit),
            "shnyr" => new CatSpot(new Vector2(0.31f, 0.275f), CatClip.Sit, faceLeft: true),
            _ => new CatSpot(new Vector2(0.4f + 0.1f * index, 0.28f), CatClip.Sit),
        };
    }
}

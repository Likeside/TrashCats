using TrashCats.Domain;
using UnityEngine;

namespace TrashCats.Presentation.Hideout
{
    /// <summary>
    /// Where things stand in the hideout, in the 0..1 space of the background picture bg_hideout (x to the right, y up;
    /// 1024x1536, Docs/ArtDirection/Generated/raw_v3/bg_hideout.png): the back wall with the door, its threshold at
    /// y ≈ 0.35, a pipe on the upper left, ground below. Props are rects; cats are foot points next to their station with
    /// the clip they play, the side they face and, for cats that are on or inside a prop, the prop they draw over.
    /// </summary>
    public static class HideoutLayout
    {
        public const float PictureAspect = 1024f / 1536f;

        /// <summary>Crop the sky first, keep the ground.</summary>
        public static readonly Vector2 Focus = new Vector2(0.5f, 0.2f);

        /// <summary>Picture height per sprite pixel at the front edge: a sitting cat (312 px) is 0.2 of the picture.</summary>
        public const float HeightPerPixel = 0.25f / 312f;

        public static float DepthScale(float y) => Mathf.Lerp(1f, 0.7f, Mathf.InverseLerp(0.08f, 0.45f, y));

        public enum CatClip
        {
            Sit,
            Sleep,
            Walk,
            Run,
            Punch,
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

            public CatSpot(Vector2 foot, CatClip clip, bool faceLeft = false, bool overProp = false, float scale = 1f, bool hidden = false)
            {
                Foot = foot;
                Clip = clip;
                FaceLeft = faceLeft;
                OverProp = overProp;
                Scale = scale;
                Hidden = hidden;
            }
        }

        /// <summary>Prop rect (min, max) for a station.</summary>
        public static (Vector2 Min, Vector2 Max) Station(ActivityDefinition activity) => activity.Kind switch
        {
            ActivityKind.Scavenge => (new Vector2(0.84f, 0.68f), new Vector2(1.0f, 0.8f)),     // the street gap on the right
            ActivityKind.Rummage => (new Vector2(-0.17f, 0.0f), new Vector2(0.26f, 0.265f)),     // big dumpster, its edge in front
            ActivityKind.Sleep => (new Vector2(0.64f, 0.02f), new Vector2(1.0f, 0.23f)),        // big box bed, front right
            _ => activity.TrainedAttribute switch
            {
                // can on a rope: its body (lower 60% of the sprite) at the punching cat's fist height (70% of the punch frame)
                AttributeType.Strength => (new Vector2(0.08f, 0.423f), new Vector2(0.23f, 0.604f)),
                AttributeType.Agility => (new Vector2(0.04f, 0.29f), new Vector2(0.13f, 0.34f)),   // toy mouse by the wall, behind the dumpster
                _ => (new Vector2(0.5f, 0.3f), new Vector2(1.09f, 0.74f)),                        // big wheel on the right, standing on the ground
            },
        };

        /// <summary>Where the cat doing <paramref name="activity"/> stands; <paramref name="index"/> spreads several cats.</summary>
        public static CatSpot Spot(ActivityDefinition activity, int index)
        {
            var shift = new Vector2(-0.09f * index, -0.03f * index);
            return activity.Kind switch
            {
                ActivityKind.Scavenge => new CatSpot(new Vector2(0.93f, 0.68f), CatClip.Walk, hidden: true),
                ActivityKind.Rummage => new CatSpot(new Vector2(0.36f, 0.16f) + shift, CatClip.Walk, faceLeft: true),
                ActivityKind.Sleep => new CatSpot(new Vector2(0.82f, 0.175f) + shift * 0.5f, CatClip.Sleep, overProp: true),
                _ => activity.TrainedAttribute switch
                {
                    // the punching cat stands right of the can and hits to the left
                    // stands at arm's length: the fist tip (5% from the frame's edge) lands in the right quarter of the can
                    AttributeType.Strength => new CatSpot(new Vector2(0.48f, 0.29f) + shift, CatClip.Punch, faceLeft: true),
                    AttributeType.Agility => new CatSpot(new Vector2(0.24f, 0.31f) + shift, CatClip.Walk, faceLeft: true),
                    // on the lowest middle point of the drum's inner floor (x 0.48, y 0.2 of the wheel sprite);
                    // run frames keep ~8% empty space under the feet, hence a little lower
                    _ => new CatSpot(new Vector2(0.783f, 0.372f), CatClip.Run, faceLeft: true, overProp: true),
                },
            };
        }

        /// <summary>Idle cats hang around in front of the door.</summary>
        public static CatSpot Idle(int index) => new CatSpot(index switch
        {
            // the free middle ground in front, away from the can, the wheel and the bed
            0 => new Vector2(0.46f, 0.05f),
            1 => new Vector2(0.4f, 0.15f),
            2 => new Vector2(0.55f, 0.13f),
            _ => new Vector2(0.45f + 0.06f * (index - 3), 0.1f),
        }, CatClip.Sit);
    }
}

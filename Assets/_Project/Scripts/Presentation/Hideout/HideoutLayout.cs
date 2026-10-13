using TrashCats.Domain;
using UnityEngine;

namespace TrashCats.Presentation.Hideout
{
    /// <summary>
    /// Where things stand in the hideout, in the 0..1 space of the background picture bg_hideout (x to the right, y up;
    /// 1024x1536, Docs/ArtDirection/Generated/raw_v2/bg_hideout.png): the back wall and door stand on y ≈ 0.40, the
    /// ground is everything below, the street passage is at the right edge. Props are rects standing on their base line;
    /// cats are foot points next to the station of their activity.
    /// </summary>
    public static class HideoutLayout
    {
        public const float PictureAspect = 1024f / 1536f;

        /// <summary>Crop the sky first, keep the ground.</summary>
        public static readonly Vector2 Focus = new Vector2(0.5f, 0.2f);

        /// <summary>Cat height (share of the picture height) at the front edge; farther cats are smaller.</summary>
        public const float CatHeight = 0.2f;

        public static float DepthScale(float y) => Mathf.Lerp(1f, 0.7f, Mathf.InverseLerp(0.08f, 0.45f, y));

        /// <summary>Prop rect (min, max) for a station.</summary>
        public static (Vector2 Min, Vector2 Max) Station(ActivityDefinition activity) => activity.Kind switch
        {
            ActivityKind.Scavenge => (new Vector2(0.78f, 0.42f), new Vector2(0.95f, 0.58f)), // the passage to the street
            ActivityKind.Rummage => (new Vector2(-0.02f, 0.13f), new Vector2(0.30f, 0.35f)), // dumpster, front left
            ActivityKind.Sleep => (new Vector2(0.66f, 0.09f), new Vector2(1.0f, 0.32f)),     // box bed, front right
            _ => activity.TrainedAttribute switch
            {
                AttributeType.Strength => (new Vector2(0.03f, 0.56f), new Vector2(0.21f, 0.85f)), // can on a wall bracket
                AttributeType.Agility => (new Vector2(0.50f, 0.21f), new Vector2(0.63f, 0.29f)),  // toy mouse in the middle of the yard
                _ => (new Vector2(0.58f, 0.38f), new Vector2(0.82f, 0.63f)),                    // wheel by the passage
            },
        };

        /// <summary>Foot point of a cat doing the activity; <paramref name="index"/> spreads several cats.</summary>
        public static Vector2 CatSpot(ActivityDefinition activity, int index)
        {
            var spot = activity.Kind switch
            {
                ActivityKind.Scavenge => new Vector2(0.86f, 0.44f),
                ActivityKind.Rummage => new Vector2(0.36f, 0.13f),
                ActivityKind.Sleep => new Vector2(0.83f, 0.25f),
                _ => activity.TrainedAttribute switch
                {
                    AttributeType.Strength => new Vector2(0.21f, 0.39f),
                    AttributeType.Agility => new Vector2(0.40f, 0.21f),
                    _ => new Vector2(0.70f, 0.42f),
                },
            };
            return spot + new Vector2(-0.08f * index, -0.025f * index);
        }

        /// <summary>Idle cats hang around in front of the door.</summary>
        public static Vector2 IdleSpot(int index) => index switch
        {
            0 => new Vector2(0.44f, 0.35f),
            1 => new Vector2(0.56f, 0.32f),
            2 => new Vector2(0.33f, 0.29f),
            _ => new Vector2(0.45f + 0.08f * (index - 3), 0.25f),
        };
    }
}

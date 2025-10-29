using HarmonyLib;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewValley;
using StardewValley.Objects;
using System;
using System.Reflection;

namespace CustomFurniture.Overrides
{
    [HarmonyPatch]
    public class FurnitureFix
    {
        internal static MethodInfo TargetMethod()
        {
            return AccessTools.Method(typeof(Furniture), "drawAtNonTileSpot");
        }

        internal static bool Prefix(Furniture __instance, SpriteBatch spriteBatch, Vector2 location, float layerDepth, float alpha = 1f)
        {
            if (__instance is CustomFurniture ho)
            {
                if (ho.texture == null)
                    ho.setTexture();
                
                if (ho.texture != null)
                {
                    // In SDV 1.6, use spriteBatch.Draw instead of a custom harmony draw method
                    spriteBatch.Draw(
                        ho.texture,
                        location,
                        ho.sourceRect.Value,
                        Color.White * alpha,
                        0f,
                        Vector2.Zero,
                        4f, // Game1.pixelZoom is typically 4
                        ho.Flipped ? SpriteEffects.FlipHorizontally : SpriteEffects.None,
                        layerDepth
                    );
                }
                return false;
            }

            return true;
        }
    }

    [HarmonyPatch]
    public class FurnitureFix2
    {
        internal static MethodInfo TargetMethod()
        {
            // SDV 1.6 simplified assembly references
            return AccessTools.Method(typeof(Furniture), "rotate");
        }

        internal static bool Prefix(Furniture __instance)
        {
            if (__instance is CustomFurniture cf)
            {
                cf.customRotate();
                return false;
            }

            return true;
        }
    }

    [HarmonyPatch]
    public class FurnitureFix3
    {
        internal static MethodInfo TargetMethod()
        {
            return AccessTools.PropertyGetter(typeof(Furniture), "placementRestriction");
        }

        internal static bool Prefix(Furniture __instance, ref int __result)
        {
            if (__instance is CustomFurniture)
            {
                __result = 0;
                return false;
            }

            return true;
        }
    }
}

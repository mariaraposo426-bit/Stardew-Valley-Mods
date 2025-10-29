using StardewModdingAPI;
using StardewModdingAPI.Events;
using Microsoft.Xna.Framework;
using System.IO;
using System.Collections.Generic;
using StardewValley;
using StardewValley.Menus;
using System.Linq;
using StardewValley.Objects;
using System;
using HarmonyLib;
using System.Reflection;
using Microsoft.Xna.Framework.Graphics;
using SpaceShared.APIs;
using StardewValley.GameData.Shops;
using StardewValley.Internal;

namespace CustomFurniture
{
    public class CustomFurnitureMod : Mod
    {
        internal static IModHelper helper;
        internal static Dictionary<string,CustomFurniture> furniture = new Dictionary<string, CustomFurniture>();
        internal static Dictionary<string, CustomFurniture> furniturePile = new Dictionary<string, CustomFurniture>();
        public static Mod instance;

        public override void Entry(IModHelper helper)
        {
            instance = this;
            CustomFurnitureMod.helper = helper;
            try
            {
                harmonyFix();
            }
            catch (Exception e)
            {
                Monitor.Log("Harmony Error: Custom deco won't work on tables." + e.StackTrace, LogLevel.Error);
            }
            helper.Events.GameLoop.GameLaunched += GameLoop_GameLaunched;
            helper.Events.GameLoop.SaveLoaded += OnSaveLoaded;
            helper.Events.GameLoop.ReturnedToTitle += OnReturnedToTitle;
            helper.ConsoleCommands.Add("cfreplace", "Triggers Custom Furniture Replacement", (s,p) => MigrateFurniture());
            helper.Events.GameLoop.DayStarted += (s, p) => MigrateFurniture();
            helper.Events.GameLoop.Saving += (s, p) =>
            {
                if(Context.IsMainPlayer)
                    Helper.Data.WriteSaveData<string>("cfreplace", "true");
            };
        }

        private void MigrateFurniture()
        {
            if (!Context.IsMainPlayer || Helper.Data.ReadSaveData<string>("cfreplace") == "true")
                return;

            Monitor.Log("Replacing Custom Furniture", LogLevel.Info);

            PyTkMigrator.MigrateItems("CustomFurniture.CustomFurniture,  CustomFurniture", (i, obj) =>
            {
                if (furniturePile.ContainsKey(i["id"]))
                {
                    var replacement = furniture[i["id"]].getOne();
                    if (replacement is CustomFurniture cf)
                    {
                        var data = new Dictionary<string, string>(i);
                        cf.rebuild(data, obj);
                        return cf;
                    }
                }

                return null;
            });
           
            PyTkMigrator.MigrateFurniture("CustomFurniture.CustomFurniture,  CustomFurniture", (i,obj) => { 
                if (furniturePile.ContainsKey(i["id"]))
                {
                    var replacement = furniture[i["id"]].getOne();
                    if(replacement is CustomFurniture cf)
                    {
                        var data = new Dictionary<string, string>(i);
                        cf.rebuild(data, obj);
                        return cf;
                    }
                }

                return null;
            });

        }

        private void GameLoop_GameLaunched(object sender, GameLaunchedEventArgs e)
        {
            var spaceCore = this.Helper.ModRegistry.GetApi<ISpaceCoreApi>("spacechase0.SpaceCore");
            spaceCore.RegisterSerializerType(typeof(CustomFurniture));
            helper.Events.GameLoop.UpdateTicked += GameLoop_UpdateTicked;
        }

        private void GameLoop_UpdateTicked(object sender, UpdateTickedEventArgs e)
        {
            loadPacks();
            helper.Events.GameLoop.UpdateTicked -= GameLoop_UpdateTicked;
        }

        public void harmonyFix()
        {
            var instance = new Harmony("Platonymous.CustomFurniture");
            instance.PatchAll(Assembly.GetExecutingAssembly());
        }

        public static void harmonyDraw(Texture2D texture, Vector2 location, Rectangle? sourceRectangle, Color color, float rotation, Vector2 origin, float scale, SpriteEffects spriteeffects, float layerDepth)
        {
            Game1.spriteBatch.Draw(texture, location, sourceRectangle, color, rotation, origin, scale, spriteeffects, layerDepth);
        }

        public static void log(string text)
        {
            instance.Monitor.Log(text,LogLevel.Trace);
        }

        private void OnReturnedToTitle(object sender, ReturnedToTitleEventArgs e)
        {
            Helper.Events.Content.AssetRequested -= OnAssetRequested;
        }

        private void OnSaveLoaded(object sender, SaveLoadedEventArgs e)
        {
            Helper.Events.Content.AssetRequested += OnAssetRequested;
        }

        private void OnAssetRequested(object sender, AssetRequestedEventArgs e)
        {
            // Add custom furniture to shop data
            if (e.NameWithoutLocale.IsEquivalentTo("Data/Shops"))
            {
                e.Edit(asset =>
                {
                    var data = asset.AsDictionary<string, ShopData>().Data;
                    
                    // Add to furniture catalogue
                    if (data.TryGetValue("Catalogue", out var catalogueData))
                    {
                        AddFurnitureToShop(catalogueData, isCatalogue: true);
                    }
                    
                    // Add to Robin's shop
                    if (data.TryGetValue("Carpenter", out var carpenterData))
                    {
                        AddFurnitureToShop(carpenterData, shopkeeper: "Robin");
                    }
                    
                    // You can add more shops here as needed
                });
            }
        }

        private void AddFurnitureToShop(ShopData shopData, bool isCatalogue = false, string shopkeeper = null)
        {
            if (shopData.Items == null)
                shopData.Items = new List<ShopItemData>();

            foreach (CustomFurniture f in furniture.Values)
            {
                if (!f.data.sellAtShop)
                    continue;

                // Check conditions if specified
                if (f.data.conditions != "none" && !string.IsNullOrEmpty(f.data.conditions))
                {
                    // Skip for now - conditions would need to be converted to 1.6 format
                    continue;
                }

                // Check if this furniture should be in this shop
                bool shouldAdd = false;
                if (isCatalogue)
                {
                    shouldAdd = true;
                }
                else if (shopkeeper != null && f.data.shopkeeper == shopkeeper)
                {
                    shouldAdd = true;
                }

                if (shouldAdd)
                {
                    var shopItem = new ShopItemData
                    {
                        ItemId = f.ItemId,
                        Price = isCatalogue ? 0 : f.data.price,
                        AvailableStock = int.MaxValue,
                        IsRecipe = false
                    };

                    shopData.Items.Add(shopItem);
                }
            }
        }

        private Api api;
        public override object GetApi()
        {
            return api ?? (api = new Api());
        }

        public Dictionary<IManifest, List<string>> furnitureByContentPack =
          new Dictionary<IManifest, List<string>>();

        private void loadPacks()
        {
            int countPacks = 0;
            int countObjects = 0;

            var contentPacks = Helper.ContentPacks.GetOwned();

            foreach (IContentPack cpack in contentPacks)
            {
                string[] cfiles = parseDir(cpack.DirectoryPath, "*.json");

                countPacks += (cfiles.Length - 1);

                foreach (string file in cfiles)
                {
                    if (file.ToLower().Contains("manifest.json") || file.ToLower().EndsWith("pytk.json"))
                        continue;

                    CustomFurniturePack pack = cpack.ReadJsonFile<CustomFurniturePack>(Path.GetFileName(file));

                    if (pack == null)
                    {
                        Monitor.Log($"Failed to load pack from {file}", LogLevel.Warn);
                        continue;
                    }

                    pack.author = cpack.Manifest.Author;
                    pack.version = cpack.Manifest.Version.ToString();
                    string author = pack.author == "none" ? "" : " by " + pack.author;
                    Monitor.Log(pack.name + " " + pack.version + author, LogLevel.Info);
                    
                    if (!furnitureByContentPack.ContainsKey(cpack.Manifest))
                    {
                      furnitureByContentPack.Add(cpack.Manifest, new List<string>());
                    }
                    
                    foreach (CustomFurnitureData data in pack.furniture)
                    {
                        countObjects++;
                        data.folderName = pack.useid == "none" ? cpack.Manifest.UniqueID : pack.useid;
                        string pileID = data.folderName + "." + Path.GetFileName(file) + "." + data.id;
                        string objectID = pileID;
                        CustomFurnitureMod.log("Load:" + objectID);
                        string tkey = $"{data.folderName}/{ data.texture}";
                        
                        if (data.textureOverlay != null)
                        {
                            string tkey2 = $"{data.folderName}/{ data.textureOverlay}";
                            if (!CustomFurniture.Textures.ContainsKey(tkey2))
                                CustomFurniture.Textures.Add(tkey2, data.fromContent ? data.textureOverlay : cpack.ModContent.GetInternalAssetName(data.textureOverlay).Name);
                        }

                        if (data.textureUnderlay != null)
                        {
                            string tkey3 = $"{data.folderName}/{ data.textureUnderlay}";
                            if (!CustomFurniture.Textures.ContainsKey(tkey3))
                                CustomFurniture.Textures.Add(tkey3, data.fromContent ? data.textureUnderlay : cpack.ModContent.GetInternalAssetName(data.textureUnderlay).Name);
                        }

                        if (!CustomFurniture.Textures.ContainsKey(tkey))
                            CustomFurniture.Textures.Add(tkey, data.fromContent ? data.texture : cpack.ModContent.GetInternalAssetName(data.texture).Name);
                        
                        CustomFurniture f = new CustomFurniture(data, objectID, Vector2.Zero);
                        furniturePile.Remove(pileID);
                        furniture.Remove(objectID);
                        furniturePile.Add(pileID, f);
                        furniture.Add(objectID, f);
                        furnitureByContentPack[cpack.Manifest].Add(f.Name);
                        
                        // Handle instant gifts
                        if (f.data.instantGift != "none")
                        {
                            // This would need to be handled differently in 1.6
                            // possibly through mail or other systems
                        }
                    }
                }

            }

            Monitor.Log(countPacks + " Content Packs with " + countObjects + " Objects found.",LogLevel.Trace);
        }

        private string[] parseDir(string path, string extension)
        {
            return Directory.GetFiles(path, extension, SearchOption.AllDirectories);
        }

        private bool meetsConditions(string conditions)
        {
            // SDV 1.6: Condition checking has changed significantly
            // This would need to be rewritten using GameStateQuery
            try
            {
                return GameStateQuery.CheckConditions(conditions);
            }
            catch
            {
                return false;
            }
        }
    }
}

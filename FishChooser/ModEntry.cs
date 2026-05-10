using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using HarmonyLib;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.ItemTypeDefinitions;
using StardewValley.Menus;

namespace FishChooser
{
    public class ModConfig
    {
        public SButton OpenMenuKey { get; set; } = SButton.Q;
        public bool ModActive { get; set; } = true;
        public bool AllowAllFish { get; set; } = false;
    }

    public interface IGenericModConfigMenuApi
    {
        void Register(IManifest mod, Action reset, Action save, bool titleScreenOnly = false);
        void AddBoolOption(IManifest mod, Func<bool> getValue, Action<bool> setValue, Func<string> name, Func<string> tooltip = null, string fieldId = null);
        void AddKeybind(IManifest mod, Func<SButton> getValue, Action<SButton> setValue, Func<string> name, Func<string> tooltip = null, string fieldId = null);
    }

    public class FishScrollMenu : IClickableMenu
    {
        private List<KeyValuePair<string, string>> options;
        private int startIndex;
        private int maxItems = 6;
        private int itemHeight = 60;
        private bool scrolling;
        private Rectangle scrollBarTrack;
        private Rectangle scrollBarRunner;
        private Rectangle randomRect;
        private Rectangle normalRect;
        private Rectangle silverRect;
        private Rectangle goldRect;
        private Rectangle iridiumRect;

        public FishScrollMenu(List<KeyValuePair<string, string>> options) : base(Game1.uiViewport.Width / 2 - 300, Game1.uiViewport.Height / 2 - 350, 600, 700, true)
        {
            this.options = options;
            upperRightCloseButton = new ClickableTextureComponent(new Rectangle(xPositionOnScreen + width - 36, yPositionOnScreen - 8, 48, 48), Game1.mouseCursors, new Rectangle(337, 494, 12, 12), 4f);
            
            scrollBarTrack = new Rectangle(xPositionOnScreen + width - 64, yPositionOnScreen + 190, 24, maxItems * itemHeight);
            UpdateRunnerPosition();

            randomRect = new Rectangle(xPositionOnScreen + 50, yPositionOnScreen + 135, 32, 32);
            normalRect = new Rectangle(xPositionOnScreen + 100, yPositionOnScreen + 135, 32, 32);
            silverRect = new Rectangle(xPositionOnScreen + 150, yPositionOnScreen + 135, 32, 32);
            goldRect = new Rectangle(xPositionOnScreen + 200, yPositionOnScreen + 135, 32, 32);
            iridiumRect = new Rectangle(xPositionOnScreen + 250, yPositionOnScreen + 135, 32, 32);
        }

        private void UpdateRunnerPosition()
        {
            if (options.Count <= maxItems)
            {
                scrollBarRunner = new Rectangle(scrollBarTrack.X, scrollBarTrack.Y, 24, 40);
                return;
            }

            float percentage = (float)startIndex / (options.Count - maxItems);
            int yPos = scrollBarTrack.Y + (int)(percentage * (scrollBarTrack.Height - 40));
            scrollBarRunner = new Rectangle(scrollBarTrack.X, yPos, 24, 40);
        }

        public override void receiveScrollWheelAction(int direction)
        {
            base.receiveScrollWheelAction(direction);
            if (direction > 0 && startIndex > 0) startIndex--;
            else if (direction < 0 && startIndex < options.Count - maxItems) startIndex++;
            UpdateRunnerPosition();
        }

        public override void receiveLeftClick(int x, int y, bool playSound = true)
        {
            base.receiveLeftClick(x, y, playSound);

            if (randomRect.Contains(x, y)) { ModEntry.SelectedQuality = -1; Game1.playSound("coin"); return; }
            if (normalRect.Contains(x, y)) { ModEntry.SelectedQuality = 0; Game1.playSound("coin"); return; }
            if (silverRect.Contains(x, y)) { ModEntry.SelectedQuality = 1; Game1.playSound("coin"); return; }
            if (goldRect.Contains(x, y)) { ModEntry.SelectedQuality = 2; Game1.playSound("coin"); return; }
            if (iridiumRect.Contains(x, y)) { ModEntry.SelectedQuality = 4; Game1.playSound("coin"); return; }

            if (upperRightCloseButton != null && upperRightCloseButton.containsPoint(x, y))
            {
                ModEntry.HelperInstance.Input.Suppress(SButton.MouseLeft);
                exitThisMenu();
                return;
            }

            if (options.Count > maxItems && scrollBarTrack.Contains(x, y))
            {
                scrolling = true;
                UpdateScrollFromMouse(y);
                return;
            }

            Rectangle cancelRect = new Rectangle(xPositionOnScreen + 40, yPositionOnScreen + height - 120, width - 120, itemHeight);
            if (cancelRect.Contains(x, y))
            {
                ModEntry.SelectedFishId = null;
                ModEntry.HelperInstance.Input.Suppress(SButton.MouseLeft);
                exitThisMenu();
                return;
            }

            for (int i = 0; i < maxItems; i++)
            {
                if (startIndex + i >= options.Count) break;
                Rectangle rect = new Rectangle(xPositionOnScreen + 40, yPositionOnScreen + 190 + (i * itemHeight), width - 120, itemHeight);
                if (rect.Contains(x, y))
                {
                    ModEntry.SelectedFishId = options[startIndex + i].Key;
                    ModEntry.HelperInstance.Input.Suppress(SButton.MouseLeft);
                    exitThisMenu();
                    return;
                }
            }
        }

        public override void leftClickHeld(int x, int y)
        {
            base.leftClickHeld(x, y);
            if (scrolling)
            {
                UpdateScrollFromMouse(y);
            }
        }

        public override void releaseLeftClick(int x, int y)
        {
            base.releaseLeftClick(x, y);
            scrolling = false;
        }

        private void UpdateScrollFromMouse(int y)
        {
            if (options.Count <= maxItems) return;

            float percentage = (float)(y - scrollBarTrack.Y) / scrollBarTrack.Height;
            percentage = Math.Clamp(percentage, 0f, 1f);
            startIndex = (int)(percentage * (options.Count - maxItems));
            UpdateRunnerPosition();
        }

        public override void draw(SpriteBatch b)
        {
            Game1.drawDialogueBox(xPositionOnScreen, yPositionOnScreen, width, height, false, true);
            upperRightCloseButton?.draw(b);

            Utility.drawTextWithShadow(b, ModEntry.Translator.Get("menu.choose-item"), Game1.dialogueFont, new Vector2(xPositionOnScreen + 50, yPositionOnScreen + 96), Color.Black);

            Color cR = ModEntry.SelectedQuality == -1 ? Color.White : Color.White * 0.4f;
            b.Draw(Game1.mouseCursors, new Vector2(randomRect.X, randomRect.Y), new Rectangle(381, 361, 10, 10), cR, 0f, Vector2.Zero, 3.2f, SpriteEffects.None, 1f);

            Color c0 = ModEntry.SelectedQuality == 0 ? Color.Black : Color.Black * 0.4f;
            b.Draw(Game1.mouseCursors, new Vector2(normalRect.X, normalRect.Y), new Rectangle(338, 400, 8, 8), c0, 0f, Vector2.Zero, 4f, SpriteEffects.None, 1f);

            Color c1 = ModEntry.SelectedQuality == 1 ? Color.White : Color.White * 0.4f;
            b.Draw(Game1.mouseCursors, new Vector2(silverRect.X, silverRect.Y), new Rectangle(338, 400, 8, 8), c1, 0f, Vector2.Zero, 4f, SpriteEffects.None, 1f);

            Color c2 = ModEntry.SelectedQuality == 2 ? Color.White : Color.White * 0.4f;
            b.Draw(Game1.mouseCursors, new Vector2(goldRect.X, goldRect.Y), new Rectangle(346, 400, 8, 8), c2, 0f, Vector2.Zero, 4f, SpriteEffects.None, 1f);

            Color c4 = ModEntry.SelectedQuality == 4 ? Color.White : Color.White * 0.4f;
            b.Draw(Game1.mouseCursors, new Vector2(iridiumRect.X, iridiumRect.Y), new Rectangle(346, 392, 8, 8), c4, 0f, Vector2.Zero, 4f, SpriteEffects.None, 1f);

            for (int i = 0; i < maxItems; i++)
            {
                if (startIndex + i >= options.Count) break;
                int yPos = yPositionOnScreen + 190 + (i * itemHeight);
                bool hovered = new Rectangle(xPositionOnScreen + 40, yPos, width - 120, itemHeight).Contains(Game1.getMouseX(), Game1.getMouseY());
                Color color = hovered ? Color.DarkOrange : Color.Black;
                Utility.drawTextWithShadow(b, options[startIndex + i].Value, Game1.dialogueFont, new Vector2(xPositionOnScreen + 50, yPos), color);
            }

            bool cancelHovered = new Rectangle(xPositionOnScreen + 40, yPositionOnScreen + height - 120, width - 120, itemHeight).Contains(Game1.getMouseX(), Game1.getMouseY());
            Utility.drawTextWithShadow(b, ModEntry.Translator.Get("menu.disable-normal-fishing"), Game1.dialogueFont, new Vector2(xPositionOnScreen + 50, yPositionOnScreen + height - 120), cancelHovered ? Color.Red : Color.DarkRed);
            if (options.Count > maxItems)
            {
                IClickableMenu.drawTextureBox(b, Game1.mouseCursors, new Rectangle(403, 383, 6, 6), scrollBarTrack.X, scrollBarTrack.Y, scrollBarTrack.Width, scrollBarTrack.Height, Color.White, 4f, false);
                b.Draw(Game1.mouseCursors, scrollBarRunner, new Rectangle(435, 463, 6, 10), Color.White);
            }

            drawMouse(b);
        }
    }

    public class ModEntry : Mod
    {
        public static ModConfig Config;
        public static string SelectedFishId = null;
        public static int SelectedQuality = -1;
        public static ITranslationHelper Translator;
        public static IModHelper HelperInstance;
        private static readonly List<string> TrashIds = new List<string> { "(O)167", "(O)168", "(O)169", "(O)170", "(O)171", "(O)172" };
        public override void Entry(IModHelper helper)
        {
            Config = helper.ReadConfig<ModConfig>();
            Translator = helper.Translation;
            HelperInstance = helper;
            helper.Events.GameLoop.GameLaunched += OnGameLaunched;
            helper.Events.Input.ButtonPressed += OnButtonPressed;
            helper.Events.GameLoop.UpdateTicked += OnUpdateTicked;

            var harmony = new Harmony(ModManifest.UniqueID);
            harmony.Patch(
                original: AccessTools.Method(typeof(GameLocation), nameof(GameLocation.getFish)),
                postfix: new HarmonyMethod(typeof(ModEntry), nameof(GetFishPostfix))
            );
            harmony.Patch(
                original: AccessTools.Method(typeof(Farm), nameof(Farm.getFish)),
                postfix: new HarmonyMethod(typeof(ModEntry), nameof(GetFishPostfix))
            );
        }

        private void OnGameLaunched(object sender, GameLaunchedEventArgs e)
        {
            var configMenu = Helper.ModRegistry.GetApi<IGenericModConfigMenuApi>("spacechase0.GenericModConfigMenu");
            if (configMenu is null) return;

            configMenu.Register(ModManifest, () => Config = new ModConfig(), () => Helper.WriteConfig(Config));
            configMenu.AddBoolOption(ModManifest, () => Config.ModActive, val => Config.ModActive = val, () => Translator.Get("config.mod-active"));
            configMenu.AddBoolOption(ModManifest, () => Config.AllowAllFish, val => Config.AllowAllFish = val, () => Translator.Get("config.allow-all-fish"));
            configMenu.AddKeybind(ModManifest, () => Config.OpenMenuKey, val => Config.OpenMenuKey = val, () => Translator.Get("config.open-menu-key"));
        }

        private void OnUpdateTicked(object sender, UpdateTickedEventArgs e)
        {
            if (!Config.ModActive || string.IsNullOrEmpty(SelectedFishId)) return;
            if (SelectedQuality == -1) return;
            if (Game1.player.mostRecentlyGrabbedItem is StardewValley.Object obj && obj.QualifiedItemId == SelectedFishId)
            {
                obj.Quality = SelectedQuality;
            }
        }

        private void OnButtonPressed(object sender, ButtonPressedEventArgs e)
        {
            bool isFishing = Game1.player.CurrentTool is StardewValley.Tools.FishingRod;
            if (!(Context.IsPlayerFree || isFishing) || Game1.activeClickableMenu != null || !Config.ModActive || e.Button != Config.OpenMenuKey) return;

            GameLocation mapaAtual = Game1.currentLocation;
            if (mapaAtual == null) return;

            List<string> listaFinalDePeixes = BuscarPeixesDoMapa(mapaAtual);
            if (listaFinalDePeixes.Count == 0) return;

            MostrarTelaDeEscolha(listaFinalDePeixes);
        }

        public static List<string> BuscarPeixesDoMapa(GameLocation espaco)
        {
            List<string> peixesAprovados = new List<string>();

            if (Config.AllowAllFish)
            {
                var todosOsMapas = Game1.content.Load<Dictionary<string, StardewValley.GameData.Locations.LocationData>>("Data\\Locations");
                foreach (var loc in todosOsMapas.Values)
                {
                    if (loc.Fish != null)
                    {
                        foreach (var candidato in loc.Fish)
                        {
                            if (string.IsNullOrEmpty(candidato.ItemId) || candidato.ItemId.Contains(" ")) continue;
                            string idFormatado = candidato.ItemId.StartsWith("(O)") ? candidato.ItemId : "(O)" + candidato.ItemId;
                            
                            if (ItemRegistry.Exists(idFormatado) && !peixesAprovados.Contains(idFormatado))
                            {
                                peixesAprovados.Add(idFormatado);
                            }
                        }
                    }
                }
            }
            else
            {
                var informacaoDoMapa = espaco.GetData();
                if (informacaoDoMapa?.Fish != null)
                {
                    foreach (var candidato in informacaoDoMapa.Fish)
                    {
                        if (string.IsNullOrEmpty(candidato.ItemId) || candidato.ItemId.Contains(" ")) continue;
                        
                        string idFormatado = candidato.ItemId.StartsWith("(O)") ? candidato.ItemId : "(O)" + candidato.ItemId;
                        if (!ItemRegistry.Exists(idFormatado)) continue;

                        if (!peixesAprovados.Contains(idFormatado))
                        {
                            bool liberado = true;

                            if (liberado && candidato.Season.HasValue && candidato.Season != Game1.season) liberado = false;

                            if (liberado && !string.IsNullOrEmpty(candidato.Condition))
                            {
                                liberado = GameStateQuery.CheckConditions(candidato.Condition, espaco, Game1.player);
                            }

                            if (liberado && candidato.CatchLimit > -1)
                            {
                                string idLimpo = idFormatado.Substring(3);
                                string idQualificado = idFormatado;

                                if (Game1.player.fishCaught != null)
                                {
                                    int[] fisgados = null;

                                    if (Game1.player.fishCaught.TryGetValue(idQualificado, out int[] f1))
                                    {
                                        fisgados = f1;
                                    }
                                    else if (Game1.player.fishCaught.TryGetValue(idLimpo, out int[] f2))
                                    {
                                        fisgados = f2;
                                    }

                                    if (fisgados != null && fisgados.Length > 0 && fisgados[0] >= candidato.CatchLimit)
                                    {
                                        liberado = false;
                                    }
                                }
                            }

                            if (liberado && !string.IsNullOrEmpty(candidato.SetFlagOnCatch))
                            {
                                if (Game1.player.mailReceived.Contains(candidato.SetFlagOnCatch)) liberado = false;
                            }

                            if (liberado)
                            {
                                peixesAprovados.Add(idFormatado);
                            }
                        }
                    }
                }
            }

            foreach (string lixoId in TrashIds)
            {
                if (!peixesAprovados.Contains(lixoId)) peixesAprovados.Add(lixoId);
            }

            return peixesAprovados;
        }

        private void MostrarTelaDeEscolha(List<string> idsAprovados)
        {
            List<KeyValuePair<string, string>> dicionarioDeNomes = new List<KeyValuePair<string, string>>();

            foreach (string idGarantido in idsAprovados)
            {
                ParsedItemData dadosDoItem = ItemRegistry.GetDataOrErrorItem(idGarantido);
                dicionarioDeNomes.Add(new KeyValuePair<string, string>(idGarantido, dadosDoItem.DisplayName));
            }

            dicionarioDeNomes.Sort((a, b) => string.Compare(LimparAcentuacao(a.Value), LimparAcentuacao(b.Value), StringComparison.OrdinalIgnoreCase));

            Game1.activeClickableMenu = new FishScrollMenu(dicionarioDeNomes);
        }

        private static string LimparAcentuacao(string textoOriginal)
        {
            var textoNormalizado = textoOriginal.Normalize(NormalizationForm.FormD);
            var montador = new StringBuilder();

            foreach (var letra in textoNormalizado)
            {
                if (CharUnicodeInfo.GetUnicodeCategory(letra) != UnicodeCategory.NonSpacingMark)
                {
                    montador.Append(letra);
                }
            }

            return montador.ToString().Normalize(NormalizationForm.FormC);
        }

        public static void GetFishPostfix(Farmer who, ref Item __result)
        {
            if (!Config.ModActive || string.IsNullOrEmpty(SelectedFishId)) return;
            if (who == null || !who.IsLocalPlayer) return;
            if (!(who.CurrentTool is StardewValley.Tools.FishingRod)) return;

            if (!Config.AllowAllFish)
            {
                List<string> peixesPermitidos = BuscarPeixesDoMapa(Game1.currentLocation);
                if (!peixesPermitidos.Contains(SelectedFishId))
                {
                    SelectedFishId = null;
                    return;
                }
            }

            __result = ItemRegistry.Create(SelectedFishId);
            if (__result is StardewValley.Object obj && SelectedQuality != -1)
            {
                obj.Quality = SelectedQuality;
            }
        }
    }
}
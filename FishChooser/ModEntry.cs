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
        private int maxItems = 7;
        private int itemHeight = 60;
        private bool scrolling;
        private Rectangle scrollBarTrack;
        private Rectangle scrollBarRunner;

        public FishScrollMenu(List<KeyValuePair<string, string>> options) : base(Game1.uiViewport.Width / 2 - 300, Game1.uiViewport.Height / 2 - 350, 600, 700, true)
        {
            this.options = options;
            upperRightCloseButton = new ClickableTextureComponent(new Rectangle(xPositionOnScreen + width - 36, yPositionOnScreen - 8, 48, 48), Game1.mouseCursors, new Rectangle(337, 494, 12, 12), 4f);
            
            scrollBarTrack = new Rectangle(xPositionOnScreen + width - 64, yPositionOnScreen + 150, 24, maxItems * itemHeight);
            UpdateRunnerPosition();
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

            if (upperRightCloseButton != null && upperRightCloseButton.containsPoint(x, y))
            {
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
                exitThisMenu();
                return;
            }

            for (int i = 0; i < maxItems; i++)
            {
                if (startIndex + i >= options.Count) break;
                Rectangle rect = new Rectangle(xPositionOnScreen + 40, yPositionOnScreen + 150 + (i * itemHeight), width - 120, itemHeight);
                if (rect.Contains(x, y))
                {
                    ModEntry.SelectedFishId = options[startIndex + i].Key;
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
            for (int i = 0; i < maxItems; i++)
            {
                if (startIndex + i >= options.Count) break;
                int yPos = yPositionOnScreen + 150 + (i * itemHeight);
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
        public static ITranslationHelper Translator;
        private static readonly List<string> TrashIds = new List<string> { "(O)167", "(O)168", "(O)169", "(O)170", "(O)171", "(O)172" };
        public override void Entry(IModHelper helper)
        {
            Config = helper.ReadConfig<ModConfig>();
            Translator = helper.Translation;
            helper.Events.GameLoop.GameLaunched += OnGameLaunched;
            helper.Events.Input.ButtonPressed += OnButtonPressed;

            var harmony = new Harmony(ModManifest.UniqueID);
            harmony.Patch(
                original: AccessTools.Method(typeof(GameLocation), nameof(GameLocation.getFish)),
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

        private void OnButtonPressed(object sender, ButtonPressedEventArgs e)
        {
            if (!Context.IsPlayerFree || Game1.activeClickableMenu != null || !Config.ModActive || e.Button != Config.OpenMenuKey) return;

            GameLocation mapaAtual = Game1.currentLocation;
            if (mapaAtual == null) return;

            List<string> listaFinalDePeixes = BuscarPeixesDoMapa(mapaAtual);
            if (listaFinalDePeixes.Count == 0) return;

            MostrarTelaDeEscolha(listaFinalDePeixes);
        }

        private List<string> BuscarPeixesDoMapa(GameLocation espaco)
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

        public static void GetFishPostfix(ref Item __result)
        {
            if (!Config.ModActive || string.IsNullOrEmpty(SelectedFishId)) return;

            __result = ItemRegistry.Create(SelectedFishId);
            SelectedFishId = null;
        }
    }
}
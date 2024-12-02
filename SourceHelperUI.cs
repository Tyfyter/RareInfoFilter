using ItemSourceHelper.Default;
using ItemSourceHelper;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Terraria.GameContent;
using Terraria.ID;
using Terraria;
using Microsoft.Xna.Framework;
using Terraria.ModLoader;
using Terraria.UI;
using Terraria.ModLoader.UI;
using Tyfyter.Utils;
using Terraria.ObjectData;
using Newtonsoft.Json.Linq;
using Terraria.IO;
using static Terraria.GameContent.TextureAssets;
using Terraria.GameContent.Bestiary;
using ItemSourceHelper.Core;

namespace RareInfoFilter {
	[ExtendsFromMod(nameof(ItemSourceHelper))]
	public class MetalDetectorFilterBrowserWindow : WindowElement {
		public SearchGridItem SearchItem { get; private set; }
		public RareTileListGridItem LootList { get; private set; }
		public FilteredEnumerable<MetalDetectorEntry> ActiveTileFilters { get; private set; }
		public override Color BackgroundColor => SourceHelperUIConfig.Instance.MetalDetectorWindowColor;
		readonly List<MetalDetectorEntry> entries = [];
		public override void SetDefaults() {
			sortOrder = -0.25f;
			ActiveTileFilters = new();
			items = new() {
				[2] = LootList = new() {
					things = ActiveTileFilters,
					colorFunc = () => SourceHelperUIConfig.Instance.MetalDetectorWindowColor
				},
				[6] = SearchItem = new(ActiveTileFilters)
			};
			itemIDs = new int[1, 2] {
				{ 6, 2 }
			};
			WidthWeights = new([1f]);
			HeightWeights = new([0f, 1f]);
			MinWidths = new([403]);
			MinHeights = new([31, 245]);
			Main.instance.LoadItem(ItemID.MetalDetector);
			texture = TextureAssets.Item[ItemID.MetalDetector];
			RareInfoFilter.ISHEnabled = true;
			SearchLoader.RegisterSearchable<MetalDetectorEntry>(MetalDetectorEntry.GetSearchData);
		}
		public override void SetDefaultSortMethod() {
			ActiveTileFilters.SetBackingList(entries);
		}
		public void SetSeenTiles(List<int> tiles) {
			entries.Clear();
			entries.AddRange(tiles.Select(t => new MetalDetectorEntry(t)).Order());
		}
		public static void SeeNewTile(int type) {
			MetalDetectorFilterBrowserWindow instance = ModContent.GetInstance<MetalDetectorFilterBrowserWindow>();
			instance.entries.InsertOrdered(new(type));
			instance.ActiveTileFilters.ClearCache();
		}
	}
	public record struct MetalDetectorEntry(int Type) : IComparable<MetalDetectorEntry> {
		public readonly int CompareTo(MetalDetectorEntry other) => Main.tileOreFinderPriority[other.Type].CompareTo(Main.tileOreFinderPriority[Type]);
		public static Dictionary<string, string> GetSearchData(MetalDetectorEntry type) => new() {
			["Name"] = Lang._mapLegendCache.FromType(type).Value
		};
		public static implicit operator int(MetalDetectorEntry entry) => entry.Type;
	}
	[ExtendsFromMod(nameof(ItemSourceHelper))]
	public class RareTileListGridItem : ThingListGridItem<MetalDetectorEntry> {
		public override bool ClickThing(MetalDetectorEntry type, bool doubleClick) {
			HashSet<int> hiddenTileTypes = RareInfoFilter.FilterPlayer.hiddenTileTypes;
			if (!hiddenTileTypes.Remove(type)) hiddenTileTypes.Add(type);
			return false;
		}
		public override void DrawThing(SpriteBatch spriteBatch, MetalDetectorEntry type, Vector2 position, bool hovering) {
			int itemType;
			int rarity = Math.Max(Main.tileOreFinderPriority[type] / 52 - 8, ItemRarityID.White);
			switch (type) {
				case TileID.Heart:
				itemType = ItemID.None;
				rarity = ContentSamples.ItemsByType[ItemID.LifeCrystal].rare;
				break;
				case TileID.LifeFruit:
				itemType = ItemID.LifeFruit;
				break;
				default:
				itemType = TileLoader.GetItemDropFromTypeAndStyle(type);
				break;
			}
			Item item = ContentSamples.ItemsByType[itemType];
			Color color;
			if (RareInfoFilter.FilterPlayer.hiddenTileTypes.Contains(type)) {
				color = hovering ? SourceHelperUIConfig.Instance.DisabledHoveredSlotColor : SourceHelperUIConfig.Instance.DisabledSlotColor;
			} else {
				color = hovering ? ItemSourceHelperConfig.Instance.HoveredItemSlotColor : ItemSourceHelperConfig.Instance.ItemSlotColor;
			}
			UIMethods.DrawColoredItemSlot(spriteBatch, ref item, position, TextureAssets.InventoryBack13.Value, color);
			if (itemType == ItemID.None) {
				Main.instance.LoadTiles(type);
				Texture2D texture = TextureAssets.Tile[type].Value;
				if (TileObjectData.GetTileData(type, 0) is TileObjectData tileObjectData) {
					int width = tileObjectData.Width * 16;
					int height = 0;
					for (int j = 0; j < tileObjectData.Height; j++) {
						height += tileObjectData.CoordinateHeights[j];
					}
					Rectangle frame = new(0, 0, 16, 16);
					Vector2 basePosition = new(position.X - width / 2 + 26 * Main.inventoryScale, position.Y - height / 2 + 26 * Main.inventoryScale);
					Vector2 pos = new();
					for (int i = 0; i < tileObjectData.Width; i++) {
						pos.X = basePosition.X + i * 15;
						pos.Y = basePosition.Y;
						for (int j = 0; j < tileObjectData.Height; j++) {
							frame.Height = tileObjectData.CoordinateHeights[j];
							spriteBatch.Draw(texture, pos, frame, Color.White);
							pos.Y += frame.Height - 1;
							frame.Y += frame.Height + 2;
						}
						frame.X += 16 + 2;
					}
				} else if (type == TileID.Pots) {
					Vector2 pos = new Vector2(position.X - 16 + 26 * Main.inventoryScale, position.Y - 16 + 26 * Main.inventoryScale).Floor();
					spriteBatch.Draw(texture, pos, new Rectangle(36, 0, 16, 16), Color.White);
					spriteBatch.Draw(texture, pos + Vector2.UnitX * 15f, new Rectangle(54, 0, 16, 16), Color.White);
					spriteBatch.Draw(texture, pos + Vector2.UnitY * 15f, new Rectangle(36, 18, 16, 16), Color.White);
					spriteBatch.Draw(texture, pos + Vector2.One * 15f, new Rectangle(54, 18, 16, 16), Color.White);
				} else {
					spriteBatch.Draw(texture, new Vector2(position.X - 8 + 26 * Main.inventoryScale, position.Y - 8 + 26 * Main.inventoryScale), new Rectangle(0, 0, 16, 16), Color.White);
				}
			} else {
				rarity = item.rare;
			}
			if (hovering) UIMethods.TryMouseText(Lang._mapLegendCache.FromType(type).Value, rarity);
		}
	}
	[ExtendsFromMod(nameof(ItemSourceHelper))]
	public class LifeformAnalyzerFilterBrowserWindow : WindowElement {
		public SearchGridItem SearchItem { get; private set; }
		public RareNPCListGridItem LootList { get; private set; }
		public FilteredEnumerable<LifeformAnalyzerEntry> ActiveNPCFilters { get; private set; }
		public override Color BackgroundColor => SourceHelperUIConfig.Instance.LifeformAnalyzerWindowColor;
		readonly List<LifeformAnalyzerEntry> entries = [];
		public override void SetDefaults() {
			sortOrder = -0.25f;
			ActiveNPCFilters = new();
			items = new() {
				[2] = LootList = new() {
					things = ActiveNPCFilters,
					colorFunc = () => SourceHelperUIConfig.Instance.LifeformAnalyzerWindowColor
				},
				[6] = SearchItem = new(ActiveNPCFilters)
			};
			itemIDs = new int[1, 2] {
				{ 6, 2 }
			};
			WidthWeights = new([1f]);
			HeightWeights = new([0f, 1f]);
			MinWidths = new([403]);
			MinHeights = new([31, 245]);
			Main.instance.LoadItem(ItemID.LifeformAnalyzer);
			texture = TextureAssets.Item[ItemID.LifeformAnalyzer];
			RareInfoFilter.ISHEnabled = true;
			SearchLoader.RegisterSearchable<LifeformAnalyzerEntry>(LifeformAnalyzerEntry.GetSearchData);
		}
		public override void SetDefaultSortMethod() {
			ActiveNPCFilters.SetBackingList(entries);
		}
		public void SetSeenNPCs(List<int> npcs) {
			entries.Clear();
			entries.AddRange(npcs.Select(t => new LifeformAnalyzerEntry(t)).Order());
		}
		public static void SeeNewNPC(int type) {
			LifeformAnalyzerFilterBrowserWindow instance = ModContent.GetInstance<LifeformAnalyzerFilterBrowserWindow>();
			instance.entries.InsertOrdered(new(type));
			instance.ActiveNPCFilters.ClearCache();
		}
	}
	public record struct LifeformAnalyzerEntry(int Type) : IComparable<LifeformAnalyzerEntry> {
		public readonly int CompareTo(LifeformAnalyzerEntry other) => ContentSamples.NpcsByNetId[other.Type].rarity.CompareTo(ContentSamples.NpcsByNetId[Type].rarity);
		public static Dictionary<string, string> GetSearchData(LifeformAnalyzerEntry type) => new() {
			["Name"] = Lang.GetNPCNameValue(type)
		};
		public static implicit operator int(LifeformAnalyzerEntry entry) => entry.Type;
	}
	[ExtendsFromMod(nameof(ItemSourceHelper))]
	public class RareNPCListGridItem : ThingListGridItem<LifeformAnalyzerEntry> {
		RenderTarget2D renderTarget;
		public override bool ClickThing(LifeformAnalyzerEntry type, bool doubleClick) {
			HashSet<int> hiddenNPCTypes = RareInfoFilter.FilterPlayer.hiddenNPCTypes;
			if (!hiddenNPCTypes.Remove(type)) hiddenNPCTypes.Add(type);
			return false;
		}
		public override void DrawThing(SpriteBatch spriteBatch, LifeformAnalyzerEntry type, Vector2 position, bool hovering) {
			if (renderTarget is not null && (renderTarget.Width != Main.screenWidth || renderTarget.Height != Main.screenHeight)) {
				renderTarget.Dispose();
				renderTarget = null;
			}
			renderTarget ??= new(Main.instance.GraphicsDevice, Main.screenWidth, Main.screenHeight, false, SurfaceFormat.Color, DepthFormat.None, 0, RenderTargetUsage.PreserveContents);
			Color color;
			if (RareInfoFilter.FilterPlayer.hiddenNPCTypes.Contains(type)) {
				color = hovering ? SourceHelperUIConfig.Instance.DisabledHoveredSlotColor : SourceHelperUIConfig.Instance.DisabledSlotColor;
			} else {
				color = hovering ? ItemSourceHelperConfig.Instance.HoveredItemSlotColor : ItemSourceHelperConfig.Instance.ItemSlotColor;
			}
			Item item = ContentSamples.ItemsByType[ItemID.None];
			UIMethods.DrawColoredItemSlot(spriteBatch, ref item, position, ItemSourceHelper.ItemSourceHelper.NPCDropBack.Value, color);
			BestiaryEntry bestiaryEntry = BestiaryDatabaseNPCsPopulator.FindEntryByNPCID(type);
			if (bestiaryEntry?.Icon is not null) {
				int size = (int)(52 * Main.inventoryScale);
				Rectangle rectangle = new((int)position.X, (int)position.Y, size, size);
				Rectangle screenPos = new((Main.screenWidth - size / 2) / 2, (Main.screenHeight - size / 2) / 2, size, size);
				BestiaryUICollectionInfo info = new() {
					OwnerEntry = bestiaryEntry,
					UnlockState = BestiaryEntryUnlockState.CanShowDropsWithDropRates_4
				};
				EntryIconDrawSettings settings = new() {
					iconbox = screenPos,
					IsHovered = hovering,
					IsPortrait = false
				};
				bestiaryEntry.Icon.Update(info, screenPos, settings);
				spriteBatch.End();
				spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, SamplerState.AnisotropicClamp, DepthStencilState.None, Main.Rasterizer, null, Main.UIScaleMatrix);
				Main.graphics.GraphicsDevice.SetRenderTarget(renderTarget);
				Main.graphics.GraphicsDevice.Clear(Color.Transparent);//rectangle.Contains(Main.mouseX, Main.mouseY) ? Color.Blue : Color.Red
				bestiaryEntry.Icon.Draw(info, spriteBatch, settings);
				/*spriteBatch.Draw(TextureAssets.Item[ItemID.DirtBlock].Value, Vector2.Zero, Color.White);
				spriteBatch.Draw(TextureAssets.Item[ItemID.DirtBlock].Value, new Vector2(Main.screenWidth - 16, 0), Color.White);
				spriteBatch.Draw(TextureAssets.Item[ItemID.DirtBlock].Value, new Vector2(0, Main.screenHeight - 16), Color.White);
				spriteBatch.Draw(TextureAssets.Item[ItemID.DirtBlock].Value, new Vector2(Main.screenWidth - 16, Main.screenHeight - 16), Color.White);*/
				spriteBatch.End();
				spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.AnisotropicClamp, DepthStencilState.None, Main.Rasterizer, null, Main.UIScaleMatrix);
				RenderTargetUsage renderTargetUsage = Main.graphics.GraphicsDevice.PresentationParameters.RenderTargetUsage;
				Main.graphics.GraphicsDevice.PresentationParameters.RenderTargetUsage = RenderTargetUsage.PreserveContents;
				Main.graphics.GraphicsDevice.SetRenderTarget(null);
				Main.graphics.GraphicsDevice.PresentationParameters.RenderTargetUsage = renderTargetUsage;
				//origin.Y -= 8;
				const int shrinkage = 2;
				const int padding = shrinkage + 1;
				rectangle.X += padding;
				rectangle.Y += padding;
				rectangle.Width -= padding * 2;
				rectangle.Height -= padding * 2;
				float alignment = 1;
				float npcScale = 1;
				if (type >= 0 && NPCID.Sets.ShouldBeCountedAsBoss[type] || ContentSamples.NpcsByNetId[type].boss || type == NPCID.DungeonGuardian) alignment = 0.5f;
				screenPos = new((int)((screenPos.X - screenPos.Width * 0.5f)), (int)((screenPos.Y - screenPos.Height * alignment)), (int)(screenPos.Width * 2 * npcScale), (int)(screenPos.Height * 2 * npcScale));
				{
					float pixelScale = (screenPos.Height / (float)rectangle.Height);
					rectangle.X -= shrinkage;
					screenPos.X -= (int)(shrinkage * pixelScale);
					rectangle.Y -= shrinkage;
					screenPos.Y -= (int)(shrinkage * pixelScale);
					rectangle.Width += shrinkage * 2;
					screenPos.Width += (int)(shrinkage * pixelScale * 2);
					rectangle.Height += shrinkage * 2;
					screenPos.Height += (int)(shrinkage * pixelScale * 2);
					spriteBatch.Draw(renderTarget, rectangle, screenPos, Color.White, 0, Vector2.Zero, SpriteEffects.None, 0);
				}
			}
			if (hovering) UIMethods.TryMouseText(Lang.GetNPCNameValue(type), (ContentSamples.NpcBestiaryRarityStars[type] - 1) * 2);
		}
	}
}

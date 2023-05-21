using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Mono.Cecil;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using ReLogic.Content;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using Terraria;
using Terraria.Audio;
using Terraria.GameInput;
using Terraria.ID;
using Terraria.Localization;
using Terraria.Map;
using Terraria.ModLoader;
using Terraria.ModLoader.Config;
using Terraria.ModLoader.Config.UI;
using Terraria.ModLoader.IO;
using Terraria.UI;

namespace RareInfoFilter {
	public class RareInfoFilter : Mod {
		public static ModKeybind OpenNPCMenuHotkey { get; private set; }
		public static ModKeybind OpenTileMenuHotkey { get; private set; }
		internal static InfoFilterPlayer FilterPlayer => Main.LocalPlayer?.GetModPlayer<InfoFilterPlayer>();
		public static AutoCastingAsset<Texture2D> SelectorEndTexture { get; private set; }
		public static AutoCastingAsset<Texture2D> SelectorMidTexture { get; private set; }
		public override void Load() {
			IL.Terraria.Main.DrawInfoAccs += Main_DrawInfoAccs;
			On.Terraria.SceneMetrics.IsValidForOreFinder += SceneMetrics_IsValidForOreFinder;
			if (Main.netMode != NetmodeID.Server) {
				SelectorEndTexture = Assets.Request<Texture2D>("Textures/UI/Selector_Back_End");
				SelectorMidTexture = Assets.Request<Texture2D>("Textures/UI/Selector_Back_Mid");
			}
			OpenNPCMenuHotkey = KeybindLoader.RegisterKeybind(this, "Open NPC Filter Menu", "NumPad7");
			OpenTileMenuHotkey = KeybindLoader.RegisterKeybind(this, "Open Tile Filter Menu", "NumPad8");
		}
		public override void Unload() {
			IL.Terraria.Main.DrawInfoAccs -= Main_DrawInfoAccs;

			SelectorEndTexture = null;
			SelectorMidTexture = null;
			OpenNPCMenuHotkey = null;
			OpenTileMenuHotkey = null;
		}
		public static void SeeNPC(NPC npc) {
			if (!FilterPlayer.seenNPCTypes.Contains(npc.type)) FilterPlayer.seenNPCTypes.Add(npc.type);
		}
		public static bool FilterNPC(NPC npc) {
			return FilterPlayer?.hiddenNPCTypes?.Contains(npc.type) ?? false;
		}
		public static void SeeTile(Tile tile) {
			if (!FilterPlayer.seenTileTypes.Contains(tile.TileType)) FilterPlayer.seenTileTypes.Add(tile.TileType);
		}
		public static bool FilterTile(Tile tile) {
			return FilterPlayer?.hiddenTileTypes?.Contains(tile.TileType) ?? false;
		}
		private static void Main_DrawInfoAccs(ILContext il) {
			ILCursor c = new(il);
			FieldReference npc = default;
			int i = default;
			ILLabel label = default;
			if (c.TryGotoNext(
				MoveType.After,
				op => op.MatchLdsfld<Main>("npc") && op.MatchLdsfld(out npc),
				op => op.MatchLdloc(out i),
				op => op.MatchLdelemRef(),
				op => op.MatchLdfld<NPC>("rarity"),
				op => op.MatchLdloc(out _),
				op => op.MatchBle(out label)
				)) {
				c.Emit(OpCodes.Ldsfld, npc);
				c.Emit(OpCodes.Ldloc, i);
				c.Emit(OpCodes.Ldelem_Ref);
				c.EmitDelegate<Func<NPC, bool>>(FilterNPC);
				c.Emit(OpCodes.Brtrue, label);

				c.GotoNext(MoveType.Before, op => op.MatchLdloc(i));
				c.Emit(OpCodes.Ldsfld, npc);
				c.Emit(OpCodes.Ldloc, i);
				c.Emit(OpCodes.Ldelem_Ref);
				c.EmitDelegate<Action<NPC>>(SeeNPC);
			} else {
				ModContent.GetInstance<RareInfoFilter>().Logger.Error("Couldn't find the npc thingy");
			}
		}
		private bool SceneMetrics_IsValidForOreFinder(On.Terraria.SceneMetrics.orig_IsValidForOreFinder orig, Tile t) {
			if (orig(t)) {
				SeeTile(t);
				return !FilterTile(t);
			}
			return false;
		}
	}
	public class FilterMenuState : UIState {
		bool isNPC = true;
		public FilterMenuState(bool isNPC = true) {
			this.isNPC = isNPC;
		}
		public override void OnInitialize() {
			FilterMenu customizationMenu = new FilterMenu(isNPC);
			Append(customizationMenu);
		}
	}
	public class FilterMenuList : UIElement {
		public override void OnInitialize() {
			Width.Set(0, 1);
		}
	}
	public class FilterMenu : UIElement {
		public float totalHeight;
		FilterMenuList listWrapper;
		FilterMenuList listWrapper2;
		bool isNPC = true;
		public FilterMenu(bool isNPC = true) {
			this.isNPC = isNPC;
		}
		public override void OnActivate() {
			SoundEngine.PlaySound(SoundID.MenuOpen);
		}
		public override void OnInitialize() {
			if (!(Elements is null)) Elements.Clear();
			Top.Pixels = 0;
			Main.UIScaleMatrix.Decompose(out Vector3 scale, out Quaternion _, out Vector3 _);
			totalHeight = 39 * scale.Y;
			UIElement element;
			int top = 6;
			InfoFilterPlayer filterPlayer = RareInfoFilter.FilterPlayer;
			if (filterPlayer is null) {
				this.Deactivate();
				Remove();
				return;
			}
			PropertyFieldWrapper[] settingList;
			if (isNPC) {
				settingList = filterPlayer
					.seenNPCTypes
					.OrderByDescending(v => ContentSamples.NpcsByNetId[v].rarity)
					.Select(v => new PropertyFieldWrapper(
						new HashSetPropertyInfo<int>($"{Lang.GetNPCNameValue(v)} ({ContentSamples.NpcsByNetId[v].rarity})", filterPlayer.hiddenNPCTypes, v)
					))
				.ToArray();
			} else {
				settingList = filterPlayer
					.seenTileTypes
					.OrderByDescending(v => Main.tileOreFinderPriority[v])
					.Select(v => new PropertyFieldWrapper(
						new HashSetPropertyInfo<int>($"{Lang.GetMapObjectName(MapHelper.TileToLookup(v, 0))} ({Main.tileOreFinderPriority[v]})", filterPlayer.hiddenTileTypes, v)
					))
				.ToArray();
			}
			Width.Set(416f * scale.X, 0);
			listWrapper = new FilterMenuList();
			listWrapper2 = new FilterMenuList();
			Tuple<UIElement, UIElement> wrapper = null;
			for (int i = 0; i < settingList.Length; i++) {
				wrapper = ConfigManager.WrapIt(this, ref top, settingList[i], filterPlayer, i, index: i);
				element = wrapper.Item2;
				//if (element is RangeElement) Width.Set(416f * scale.X, 0);
				//element.Top.Set(element.Top.Pixels + top, element.Top.Percent);
				totalHeight += top;
				listWrapper.Append(wrapper.Item1);
			}
			listWrapper.Height.Set(Height.Pixels, 0);
			listWrapper2.Append(listWrapper);

			listWrapper2.Height.Set(-(52 + 16), 1);
			listWrapper2.OverflowHidden = true;
			listWrapper2.Left.Pixels = -12;
			listWrapper2.Top.Pixels += 8;
			Append(listWrapper2);

			//Append(listWrapper2);
			Height.Set(0, 1);
			//Height.Set(Math.Min(totalHeight + (Width.Pixels / 8), Main.screenHeight * 0.9f), 0);
			//Left.Set(Width.Pixels * 0.1f, 1f);
			//Top.Set(Height.Pixels * -0.5f, 0.5f);
		}
		public override void ScrollWheel(UIScrollWheelEvent evt) {
			listWrapper.Top.Pixels = MathHelper.Clamp(listWrapper.Top.Pixels + evt.ScrollWheelValue, (Main.screenHeight - (52 + 16)) - listWrapper.Height.Pixels, 0);
			this.Recalculate();
		}
		public override void Draw(SpriteBatch spriteBatch) {
			base.Draw(spriteBatch);
		}
		protected override void DrawSelf(SpriteBatch spriteBatch) {
			Rectangle dimensions = this.GetDimensions().ToRectangle();
			if (Main.mouseY < dimensions.Width && !PlayerInput.IgnoreMouseInterface) {
				Main.LocalPlayer.mouseInterface = true;
			}
			int endHeight = dimensions.Width / 8;
			Color color = isNPC ? Color.DodgerBlue : Color.LimeGreen;
			Rectangle topRect = new Rectangle(dimensions.X, dimensions.Y, dimensions.Width, endHeight);
			Rectangle midRect = new Rectangle(dimensions.X, dimensions.Y + endHeight, dimensions.Width, dimensions.Height - (endHeight * 2));
			Rectangle bottomRect = new Rectangle(dimensions.X, dimensions.Y + dimensions.Height - endHeight, dimensions.Width, endHeight);
			spriteBatch.Draw(RareInfoFilter.SelectorEndTexture, topRect, new Rectangle(0, 0, 208, 26), color, 0, default, SpriteEffects.None, 0);
			spriteBatch.Draw(RareInfoFilter.SelectorMidTexture, midRect, new Rectangle(0, 0, 208, 1), color, 0, default, SpriteEffects.None, 0);
			spriteBatch.Draw(RareInfoFilter.SelectorEndTexture, bottomRect, new Rectangle(0, 0, 208, 26), color, 0, default, SpriteEffects.FlipVertically, 0);
			//spriteBatch.Draw(TextureAssets.InventoryBack2.Value, dimensions, null, color, 0, default, SpriteEffects.None, 0);

		}
	}
	public struct AutoCastingAsset<T> where T : class {
		public bool IsLoaded => asset?.IsLoaded ?? false;
		public T Value => asset?.Value;

		readonly Asset<T> asset;
		AutoCastingAsset(Asset<T> asset) {
			this.asset = asset;
		}
		public static implicit operator AutoCastingAsset<T>(Asset<T> asset) => new(asset);
		public static implicit operator T(AutoCastingAsset<T> asset) => asset.Value;
	}
	public class InfoFilterPlayer : ModPlayer {
		public List<int> seenNPCTypes = new();
		public HashSet<int> hiddenNPCTypes = new();

		public List<int> seenTileTypes = new();
		public HashSet<int> hiddenTileTypes = new();
		public override void SaveData(TagCompound tag) {
			tag.Add("seenNPCTypes", seenNPCTypes.Select(SerializeNPC).ToList());
			tag.Add("hiddenNPCTypes", hiddenNPCTypes.Select(SerializeNPC).ToList());

			tag.Add("seenTileTypes", seenTileTypes.Select(SerializeTile).ToList());
			tag.Add("hiddenTileTypes", hiddenTileTypes.Select(SerializeTile).ToList());
		}
		public override void LoadData(TagCompound tag) {
			if (tag.TryGet("seenNPCTypes", out List<string> _seenNPCTypes)) {
				seenNPCTypes = _seenNPCTypes.Select(DeserializeNPC).ToList();
			} else {
				seenNPCTypes = new List<int>() { };
			}
			if (tag.TryGet("hiddenNPCTypes", out List<string> _hiddenNPCTypes)) {
				hiddenNPCTypes = _hiddenNPCTypes.Select(DeserializeNPC).ToHashSet();
			} else {
				hiddenNPCTypes = new HashSet<int>() { };
			}

			if (tag.TryGet("seenTileTypes", out List<string> _seenTileTypes)) {
				seenTileTypes = _seenTileTypes.Select(DeserializeTile).ToList();
			} else {
				seenTileTypes = new List<int>() { };
			}
			if (tag.TryGet("hiddenTileTypes", out List<string> _hiddenTileTypes)) {
				hiddenTileTypes =  _hiddenTileTypes.Select(DeserializeTile).ToHashSet();
			} else {
				hiddenTileTypes = new HashSet<int>() { };
			}
		}
		public override void SetControls() {
			if (RareInfoFilter.OpenNPCMenuHotkey.JustPressed) IngameFancyUI.OpenUIState(new FilterMenuState(isNPC: true));
			if (RareInfoFilter.OpenTileMenuHotkey.JustPressed) IngameFancyUI.OpenUIState(new FilterMenuState(isNPC: false));
		}
		static string SerializeNPC(int v) {
			if (v < NPCID.Count) {
				return $"Terraria:{NPCID.Search.GetName(v)}";
			} else {
				ModNPC npc = NPCLoader.GetNPC(v);
				return $"{npc.Mod.Name}:{npc.Name}";
			}
		}
		static int DeserializeNPC(string s) {
			string[] segs = s.Split(':');
			if (segs[0] == "Terraria") {
				return NPCID.Search.GetId(segs[1]);
			} else if (ModContent.TryFind(segs[0], segs[1], out ModNPC npc)) {
				return npc.Type;
			}
			return -1;
		}
		static string SerializeTile(int v) {
			if (v < TileID.Count) {
				return $"Terraria:{TileID.Search.GetName(v)}";
			} else {
				ModTile tile = TileLoader.GetTile(v);
				return $"{tile.Mod.Name}:{tile.Name}";
			}
		}
		static int DeserializeTile(string s) {
			string[] segs = s.Split(':');
			if (segs[0] == "Terraria") {
				return TileID.Search.GetId(segs[1]);
			} else if (ModContent.TryFind(segs[0], segs[1], out ModTile tile)) {
				return tile.Type;
			}
			return -1;
		}
	}
	public class HashSetPropertyInfo<T> : PropertyInfo {
		public override System.Reflection.PropertyAttributes Attributes { get; }
		readonly HashSet<T> set;
		readonly T value;
		readonly string name;
		public override string Name => name;
		internal HashSetPropertyInfo(string name, HashSet<T> set, T value) : base() {
			this.name = name ?? "";
			this.set = set;
			this.value = value;
		}
		public bool Get() => set.Contains(value);
		public void Set(bool value) {
			if (value) {
				set.Add(this.value);
			} else {
				set.Remove(this.value);
			}
		}
		#region go no further, for here madness lies
		public override object GetValue(object obj, BindingFlags invokeAttr, Binder binder, object[] index, CultureInfo culture) {
			return Get();
		}
		public override void SetValue(object obj, object value, BindingFlags invokeAttr, Binder binder, object[] index, CultureInfo culture) {
			Set((bool)value);
		}
		public override bool CanRead => true;
		public override bool CanWrite => true;
		public override Type PropertyType => typeof(bool);
		public override Type DeclaringType { get; }
		public override Type ReflectedType { get; }

		public override MethodInfo[] GetAccessors(bool nonPublic) {
			return Array.Empty<MethodInfo>();
		}

		public override object[] GetCustomAttributes(bool inherit) {
			return Array.Empty<Attribute>();
		}

		public override object[] GetCustomAttributes(Type attributeType, bool inherit) {
			return Array.Empty<Attribute>();
		}

		public override MethodInfo GetGetMethod(bool nonPublic) => typeof(HashSetPropertyInfo<T>).GetMethod("Get", BindingFlags.Public | BindingFlags.Instance);

		public override ParameterInfo[] GetIndexParameters() {
			return Array.Empty<ParameterInfo>();
		}

		public override MethodInfo GetSetMethod(bool nonPublic) => typeof(HashSetPropertyInfo<T>).GetMethod("Set", BindingFlags.Public | BindingFlags.Instance);

		public override bool IsDefined(Type attributeType, bool inherit) => true;
		#endregion go no further, for here madness lies
	}
}
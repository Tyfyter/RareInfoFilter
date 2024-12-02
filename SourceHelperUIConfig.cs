using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Terraria.ModLoader.Config;

namespace RareInfoFilter {
	public class SourceHelperUIConfig : ModConfig {
		public override ConfigScope Mode => ConfigScope.ClientSide;
		public static SourceHelperUIConfig Instance;
		[DefaultValue(typeof(Color), "172, 128, 30, 255")]
		public Color MetalDetectorWindowColor { get; set; }
		[DefaultValue(typeof(Color), "172, 46, 128, 255")]
		public Color LifeformAnalyzerWindowColor { get; set; }

		[DefaultValue(typeof(Color), "103, 67, 50, 255")]
		public Color DisabledSlotColor { get; set; }
		[DefaultValue(typeof(Color), "132, 30, 15, 255")]
		public Color DisabledHoveredSlotColor { get; set; }
	}
}

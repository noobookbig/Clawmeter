#pragma once
#include <lvgl.h>

// Hermes-inspired design tokens used by the small-screen 2432S028 UI refresh.
// Keep the aliases below so older shared code keeps compiling while the newer
// screens can opt into the richer palette directly.
#define THEME_BG         lv_color_hex(0x000000)
#define THEME_PANEL      lv_color_hex(0x262a33)
#define THEME_PANEL_ALT  lv_color_hex(0x1c2028)
#define THEME_PANEL_EDGE lv_color_hex(0x5c6678)
#define THEME_TEXT       lv_color_hex(0xf5f1e8)
#define THEME_DIM        lv_color_hex(0xc4bfb0)
#define THEME_BLUE       lv_color_hex(0x5a7aff)
#define THEME_YELLOW     lv_color_hex(0xffd53d)
#define THEME_GREEN      lv_color_hex(0x34b55a)
#define THEME_ORANGE     lv_color_hex(0xffaa77)
#define THEME_TRACK      lv_color_hex(0x383d46)

#define THEME_ACCENT THEME_BLUE
#define THEME_AMBER  THEME_YELLOW
#define THEME_RED    THEME_ORANGE
#define THEME_BAR_BG THEME_TRACK

// 013 Landscape Neon Glow palette (cyan / magenta on dark void).
// Picked to dodge RGB565 banding: instead of smooth blends, the neon theme
// uses SHARP neon borders + box-shadow halos. See sketches/013-landscape-neon-glow.
#define THEME_NEON_CYAN     lv_color_hex(0x00e5ff)
#define THEME_NEON_MAGENTA  lv_color_hex(0xff2bd6)
// 014 Quad-Glow extension: five-colour flow (cyan/magenta/red/orange/green).
#define THEME_NEON_RED      lv_color_hex(0xff3355)
#define THEME_NEON_ORANGE   lv_color_hex(0xff9a3c)
#define THEME_NEON_GREEN    lv_color_hex(0x35e08a)
#define THEME_NEON_VOID     lv_color_hex(0x050608)  // matches COL_SCREEN
#define THEME_NEON_PANEL    lv_color_hex(0x0f1116)  // solid (no gradient)
#define THEME_NEON_PANEL_2  lv_color_hex(0x191d23)  // secondary accent bg
#define THEME_NEON_TRACK    lv_color_hex(0x1f242c)
#define THEME_NEON_GLOW_HI  lv_color_hex(0x7af6ff)  // pill text on cyan
#define THEME_NEON_GLOW_HI2 lv_color_hex(0xff7ff0)  // pill text on magenta

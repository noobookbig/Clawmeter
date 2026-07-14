#pragma once
#include <lvgl.h>

// Neon-glow-on-dark tokens (paired with sketches/013-landscape-neon-glow).
// Dark Hermes base is kept — large light surfaces band on 16-bit ILI9341 —
// cyan / magenta accents drive sharp glow halos via LVGL shadow_color.
#define THEME_BG         lv_color_hex(0x000000)
#define THEME_PANEL      lv_color_hex(0x0c0a16)
#define THEME_PANEL_ALT  lv_color_hex(0x150f24)
#define THEME_PANEL_EDGE lv_color_hex(0x2a1d5e)
#define THEME_TEXT       lv_color_hex(0xf8feff)
#define THEME_DIM        lv_color_hex(0xb5a8d8)
#define THEME_BLUE       lv_color_hex(0x00e5ff)
#define THEME_YELLOW     lv_color_hex(0xff2bd6)
#define THEME_GREEN      lv_color_hex(0x34b55a)
#define THEME_ORANGE     lv_color_hex(0xffaa77)
#define THEME_TRACK      lv_color_hex(0x1c1530)
#define THEME_GLOW_CYAN    lv_color_hex(0x00e5ff)
#define THEME_GLOW_MAGENTA lv_color_hex(0xff2bd6)

#define THEME_ACCENT THEME_BLUE
#define THEME_AMBER  THEME_YELLOW
#define THEME_RED    THEME_ORANGE
#define THEME_BAR_BG THEME_TRACK

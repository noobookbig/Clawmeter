#pragma once

#include <stddef.h>

#include "data.h"

bool usage_parse_json(const char* json, UsageData* out);
void usage_panel_display_subtext(const UsagePanelData* panel, char* buf, size_t len);

// Look for a top-level integer "brightness" field (0..100, percent). On hit,
// writes to *out_pct (clamped) and returns true. The daemon uses this to
// drive the firmware brightness without a separate BLE characteristic.
bool usage_extract_brightness_pct(const char* json, uint8_t* out_pct);

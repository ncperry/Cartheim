#!/usr/bin/env bash
# Writes the designed camp records (the format Record camp writes) for WoodHouse4-13, from their templates (see DevTemplates):
#   tools/design-camps.sh src/Cartheim.Core/Camps
out="$1"
calc() { awk "BEGIN { printf \"%.4f\", $1 }"; }
p() { printf 'player | %s | %.2f | %.2f | %.2f | %s | %s | designed\n' "$1" "$2" "$3" "$4" "$5" "$6"; }
header() { printf '# Camp at %s: designed from its template (%s), reaching %s m\n# whose | prefab | x | y | z | yaw | what it is | what'"'"'s on it\n' "$1" "$2" "$3"; }
# The cart: its left side (where its tent goes) turned away from the building.
cart() { printf 'cart | Cart | %.2f | 0.06 | %.2f | %s | Vagon,CartBase | designed\n' "$1" "$2" "$3"; }
bedspot() { printf 'building | goblin_bed | %.2f | 0.00 | %.2f | %s | designed bed spot | \n' "$1" "$2" "$3"; }
rug() { p rug_deer "$1" 0.10 "$2" "$3" "designed rug spot"; }
# A fire pit with a cooking station either side, as in the WoodHouse2 camp.
fire() { p fire_pit "$1" 0 "$2" 180 Fireplace; p piece_cookingstation "$(calc "$1 - 0.08")" 0.20 "$(calc "$2 + 0.40")" 180 CookingStation; p piece_cookingstation "$(calc "$1 - 0.07")" 0.20 "$(calc "$2 - 0.45")" 180 CookingStation; }
torch() { p piece_groundtorch_wood "$1" 0.79 "$2" 113 Fireplace; }
bench() { p piece_bench01 "$1" 0.09 "$2" "$3" Piece; }
table() { p piece_table "$1" 0.04 "$2" "$3" Piece; }
# Something set on a table (0.81 up) or a bench (0.57 up).
ontable() { p "$1" "$2" 0.85 "$3" "$4" ItemDrop; }
onbench() { p "$1" "$2" 0.66 "$3" "$4" ItemDrop; }
# A ring of sharp stakes, gapped where the way in is (angles in degrees, from +x towards +z).
stakes() { local r=$1; shift; for a in $(seq 0 22.5 337.5); do skip=0; for g in "$@"; do [ "$a" = "$g" ] && skip=1; done; [ $skip = 1 ] && continue
  x=$(calc "$r * cos($a * 3.14159265 / 180)"); z=$(calc "$r * sin($a * 3.14159265 / 180)"); yaw=$(calc "-($a + 90)"); p piece_sharpstakes "$x" 0.05 "$z" "$yaw" Piece; done; }

# A banner on two poles, facing straight out from the building's middle; hung on the poles as in the WoodHouse3 camp.
banner() { local yaw; yaw=$(calc "atan2($1, $2) * 180 / 3.14159265")
  p wood_pole2 "$1" 1.13 "$2" "$yaw" Piece; p wood_pole2 "$1" 3.13 "$2" "$yaw" Piece
  p piece_banner05 "$(calc "$1 + 0.30 * cos($yaw * 3.14159265 / 180) + 0.05 * sin($yaw * 3.14159265 / 180)")" 3.96 \
    "$(calc "$2 - 0.30 * sin($yaw * 3.14159265 / 180) + 0.05 * cos($yaw * 3.14159265 / 180)")" "$yaw" Piece; }

# Each camp's fire within 4.5 m of where its bed is (the cart's, or the bed in the building), to sleep by.

# 4: the narrow lean-to, tall as a lookout: a flag flying beside it, benches by the fire in front of the cart, a mead left out.
{ header WoodHouse4 "the lookout lean-to: a flag beside it, benches by the fire, a mead left out" 8.0
  fire -4.5 4.0; bench -2.9 4.0 90; bench -6.1 4.0 90; onbench MeadStaminaMinor -2.9 3.8 0
  torch -3.0 6.0; torch -6.0 6.0; banner 2.2 1.0; cart -4.5 0 0; } > "$out/WoodHouse4.camp"

# 5: the beekeeper's cottage: two hives of the camp's own, honey on the table to take.
{ header WoodHouse5 "the beekeeper's cottage: hives of its own, honey on the table to take" 8.0
  p piece_beehive -4.3 0 2.6 0 Beehive; p piece_beehive -4.3 0 0.6 0 Beehive
  table -0.7 4.5 0; ontable Honey -0.9 4.4 0; ontable Honey -0.6 4.6 30; ontable Honey -0.4 4.4 60; bench -0.7 5.6 0
  fire 4.6 -4.0; bench 4.6 -5.3 0; torch 3.0 -5.6; torch 6.2 -5.6; cart 4.6 0 180; } > "$out/WoodHouse5.camp"

# 6: the watchtower: a ring of stakes round it (a gap where the cart stands), the fire by the gate.
{ header WoodHouse6 "the watchtower: a ring of stakes round it, the fire by the gate" 8.0
  stakes 6.5 247.5 270.0 292.5; fire 3.4 -8.4; bench 4.9 -8.4 90; bench 3.4 -9.9 0
  torch -1.6 -6.0; torch 1.6 -6.0; banner 3.0 -3.0; cart 0 -10 -90; } > "$out/WoodHouse6.camp"

# 7: the A-frame cottage: its doorway in the gable at -x; the cart's bed against the far gable, the rug between, the fire
# outside that gable; a herb table by the door, mushrooms to take, jam to look at.
{ header WoodHouse7 "the A-frame cottage: the cart's bed and rug inside, a herb table by the door" 8.0
  rug -0.6 0 0; bedspot 1.7 0 0; fire 5.0 0
  table -5.0 3.0 90; ontable Mushroom -5.2 2.8 0; ontable Mushroom -5.0 3.1 40; ontable Mushroom -4.8 2.8 80
  ontable QueensJam -4.8 3.3 0; bench -6.2 3.0 90; onbench piece_jackoturnip -6.2 3.4 113
  torch -4.0 -2.0; torch 5.0 2.0; cart 0 7.0 90; } > "$out/WoodHouse7.camp"

# 8: the old foundations: the fire in the middle, benches round it, raspberries on a table; the cart just inside.
{ header WoodHouse8 "the old foundations: a fire in their middle, benches round it, raspberries to take" 10.0
  fire 0 0; bench 0 2.3 0; bench 0 -2.3 0; bench -2.3 0 90
  table -3.0 -3.2 0; ontable Raspberry -3.3 -3.1 0; ontable Raspberry -3.0 -3.3 60; ontable Raspberry -2.7 -3.1 120
  torch 2.5 3.5; torch -3.5 3.5; cart 3.6 -1.0 180; } > "$out/WoodHouse8.camp"

# 9: the longhouse feast: a feast laid out to look at, carrot seeds on the table to take; the fire before the cart.
{ header WoodHouse9 "the longhouse feast: a feast to look at, carrot seeds to take" 8.0
  table 0 0 0; ontable FeastMeadows 0 0 0; ontable CookedMeat -0.4 0.3 0; ontable MeadStaminaMinor 0.4 -0.3 0
  ontable CarrotSeeds 0.6 0.35 0; ontable CarrotSeeds 0.7 0.2 40; ontable CarrotSeeds -0.6 -0.35 80
  bench -1.2 0 90; bench 1.2 0 90; fire -6.2 7.0; torch -4.6 8.2; torch -7.8 8.2; cart -6.2 3.0 0; } > "$out/WoodHouse9.camp"

# 10: the fallen longhouse: a fire where its hall was, the remains of a meal, turnip seeds to take; the cart by its wall.
{ header WoodHouse10 "the fallen longhouse: a fire in its hall, the remains of a meal, turnip seeds to take" 8.0
  fire 1.0 -3.0; table 0 1.2 0; ontable NeckTailGrilled -0.3 1.1 0; ontable MeadHealthMinor 0.3 1.3 0
  ontable TurnipSeeds 0.5 1.0 0; ontable TurnipSeeds 0.6 1.2 40; ontable TurnipSeeds -0.5 1.4 80
  bench -1.2 1.2 90; bench 1.2 1.2 90; torch -2.2 -4.5; torch 2.2 -4.5; cart 4.6 -3.0 180; } > "$out/WoodHouse10.camp"

# 11: the great hall: the cart's bed and rug inside, a hearth beside them, a feast with carrot and turnip seeds.
{ header WoodHouse11 "the great hall: the cart's bed and rug inside by a hearth, a feast with seeds to take" 8.0
  rug -1.5 -0.5 90; bedspot -2.6 -0.5 90; fire -0.4 -1.9; table 0.6 0.9 0; ontable FeastMeadows 0.6 0.9 0; ontable CookedMeat 0.2 1.1 0
  ontable CarrotSeeds 1.1 0.7 0; ontable CarrotSeeds 1.2 1.0 40; ontable TurnipSeeds 0.1 0.6 0; ontable TurnipSeeds 0.0 0.8 40
  bench 0.6 1.9 0; torch 5.6 1.8; torch 5.6 -2.8; cart -7.2 -0.5 0; } > "$out/WoodHouse11.camp"

# 12: the outhouse: the camp kept well away from it, round the cart, dandelions for the curious.
{ header WoodHouse12 "the outhouse: the camp kept well away from it, dandelions on the table" 6.0
  fire 4.2 -4.0; bench 5.6 -4.0 90; table 2.8 -4.0 90; ontable Dandelion 2.8 -4.2 0; ontable Dandelion 2.9 -3.8 50
  torch 4.2 -6.0; cart 4.2 0 180; } > "$out/WoodHouse12.camp"

# 13: the overgrown hall: trees through its floor, blueberries (from the forest beyond) on the table, mushrooms underfoot.
{ header WoodHouse13 "the overgrown hall: blueberries on the table, mushrooms underfoot" 9.0
  table 6.0 2.6 0; ontable Blueberries 5.8 2.5 0; ontable Blueberries 6.0 2.7 40; ontable Blueberries 6.2 2.5 80
  p Mushroom 4.8 0.05 -2.6 0 ItemDrop; p Mushroom 5.1 0.05 -2.9 70 ItemDrop
  bench 6.0 3.7 0; fire -7.2 3.5; torch -5.6 3.0; cart -7.2 -0.5 0; } > "$out/WoodHouse13.camp"

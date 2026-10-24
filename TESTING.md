# In-game checks waiting for you

Everything here is built but not yet seen working in the game. Tick items off (or tell Claude what went wrong)
and Claude reads `BepInEx\LogOutput.log` (lines start with `[Cartheim]`) before guessing at a fix.

## Step 1: cart workbench

- [ ] Hovering a cart shows a new line: `[Shift + E] Cart`.
- [ ] Shift+E opens the Cart panel; plain E still hitches the cart.
- [ ] The panel lists Workbench costing 10 Wood + 5 Bronze nails; the button says "Need more" when you can't afford it.
- [ ] Build it: the materials leave your inventory, the button turns to "Built", and a Stations section appears (Workbench, Level 1).
- [ ] Use opens the workbench crafting screen at the cart, with no "needs a roof" message.
- [ ] Walking away from the cart (over ~5 m) closes the crafting screen, like walking away from a workbench.
- [ ] With the hammer out near the cart, workbench pieces can be placed without a real workbench nearby.
- [ ] Esc closes the Cart panel without opening the game menu.
- [ ] Log out and back in: the cart still has its workbench.
- [ ] Mods tab → switch Cartheim off: no Cart line on hover, Shift+E does nothing special, and the cart is no longer a workbench. Switch on: it is again.

## Step 3: a destroyed cart gives everything back

- [ ] The first time a cart loads, the log says `[Cartheim] Cart ... ready; health ..., has WearNTear True`.
      (If it says False, carts break some other way and the refund isn't hooked up: tell Claude.)
- [ ] Destroy a cart that has a workbench built in: alongside the cart's own wood and nails and its contents,
      10 Wood + 5 Bronze nails drop. The log says `destroyed; dropping 10 Wood, 5 BronzeNails`.
- [ ] Same again with Cartheim switched off in the Mods tab: the materials still drop.

## Step 2: every crafting station

- [ ] The Cart panel lists all stations and their extensions in the hammer's order (workbench + 4, cauldron + 6, forge + 6,
      artisan table + press, black forge + 5, galdr table + 4, food preparation table, mead ketill, stonecutter).
      Extensions show "Locked" until their station is in; everything but the workbench shows "Locked" until the workbench is in.
- [ ] Chopping block costs 10 Wood + 10 Flint + 5 Bronze nails; the cart workbench is then level 2 (panel, and the crafting screen).
- [ ] Forge in the cart: Use opens forge crafting at the cart, no roof needed. Each forge upgrade raises its level.
- [ ] Hit a cart that has a forge (e.g. with a club): the log shows `Cart armor 5: hit X -> Y` with Y lower than X.
- [ ] Each workbench level adds 100 cart health (the health bar drains more slowly as you hit it).
- [ ] Cauldron in the cart: Use says it needs fire with no fire near; light a campfire within 5 m of the cart and it works.
- [ ] Destroying a cart with several stations drops the materials of all of them.

## Step 4: handling and looks

- [ ] A cart crammed with stone pulls as easily as an empty one (Cartheim on); switch Cartheim off and within ~5 s it's heavy again.
- [ ] A bare cart looks empty even when full of items; each upgrade makes it look fuller (10 upgrades = as full as it gets).
      Switch Cartheim off and it looks as full as its contents again.

## Step 5: the cart's chest

- [ ] The Cart panel has a "Chest (one per cart)" section: Chest, Reinforced Chest, Black Metal Chest, Personal Chest,
      Grausten Chest, each costing its normal recipe plus nails. After building one, the others show "Locked".
- [ ] A "More storage" section offers the Barrel (12 slots) and Wardrobe (50 slots), one of each, alongside the chest.
      With all three built the tabs read Cart / Chest / Barrel / Wardrobe.
- [ ] Building one shows no chest anywhere on or near the cart (it rides hidden inside it). The log says `Built piece_chest_... into cart`.
- [ ] Open the cart's storage: "Cart" and "Chest" tabs appear above the storage window. Chest shows the chest's slots
      (10 / 24 / 32 / 6 / 40 for chest / reinforced / black metal / personal / grausten); Cart goes back. Items stay put on each page.
- [ ] The Cart panel also lists the chest with an Open button.
- [ ] Pull the cart a long way, then open the chest again: it is still there with its items.
- [ ] Log out and back in: chest and items still there.
- [ ] Personal chest: another player (or another character of yours) gets "can't open" on the Chest tab but can still use the Cart page.
- [ ] Destroy the cart: the chest's items spill out along with the cart's own items and every upgrade's materials (chest recipe + nails included).
- [ ] Mods tab → Cartheim off: no tabs, no Open button; switch back on and the chest and its items are back.
- [ ] Two players: one has the chest page open, the other gets "in use" (vanilla behaviour) rather than both editing it.

## Step 6: roof and hearth

- [ ] The Cart panel has a "Roof" section: Thatch roof (10 Wood + 5 Bronze nails), Shingle roof (10 Wood + 5 Tar + 5 Iron nails),
      Grausten roof (25 Grausten + 5 Iron nails). Shingle is Locked until thatch is in, grausten until shingle is.
- [ ] In the rain, standing within 5 m of a roofed cart: no Wet status, and the Shelter status shows. Step away: you get wet.
- [ ] A campfire within 5 m of a roofed cart burns full in the rain (no small damp flame, doesn't go out).
- [ ] The Cart panel has a "Fire" section with Hearth (15 Stone + 5 Bronze nails). Built: flames, light and smoke appear
      in the cart's bed, and stay lit with no firewood.
- [ ] Standing right next to the cart's hearth doesn't set you on fire.
- [ ] A cart cauldron works with the hearth built in and no campfire around.
- [ ] Cartheim off: the hearth goes out, the roof stops keeping you dry. On again: both back.

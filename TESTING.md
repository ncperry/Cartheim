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

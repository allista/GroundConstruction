#Ground Construction ChangeLog

* **v2.4.1 -- Making Resources**
    * **Parts with *selected* resources are assembled and constructed with these resources**
        * By default only two such resources are supported:
            * **Ablator** is made from MaterialKits during the contraction phase
            * **Machinery** is mdade from SpecializedParts during the assembly phase
        * Other resources may be added by other mods:
            * Add a resource name into a ﻿GC_CONSTRUCT_RESOURCES node in any of .cfg files in your mod to make that resource from MaterialKits in GC
            * Add a resource name into a ﻿GC_ASSEMBLE_RESOURCES node in any of .cfg files in your mod to make that resource from SpecializedParts in GC
    * Added patches for Mk3 ISRU from Stockalike Mining Extension **made by @ZEROX7**
    * *For modders: renamed GC_KIT_RESOURCES to GC_KEEP_RESOURCES*

* v2.4 -- **Incremental Station Construction**
    * Vessels constructed inside Orbital Kit Container **can be docked to the main vessel instead of the container** when launched.
    * **Orientation of deployment/spawning can be changed** (_in 90 degrees steps around Up axis_) from the container's part menu and from the workshop UI.
    * **Added in-editor information window** that shows assembly/construction requirements for the current ship, and shows bottom-most attach nodes available for docked construction.
    * **Deployment hint shows silhouette** of the vessel that will be constructed in addition to the size of the deployed container.
    *  **Deployment hint may be shown/hidden per container.**
    * Added part menu button to add a single part to the container in editor.
    * Added warnings that prevent accidental deployment.
    * Corrected Vessel/PartKit requirements calculations. Now resources that are not stripped away when a kit is created do not contribute to complexity and construction resource requirements.
    * A PartKit costs at least dry-part-cost*0.1.
    * Fixed kit resizing on kit creation/deployment in flight.

* v2.3.2
    * Made docked deployment compatible with **auto struts** and **Kerbal Joint Reinforcement Next >=4.0.1**
    * Added ability to change UI color scheme at runtime
        * To access the Color Scheme dialog, **right-click the GC toolbar button**

* v2.3.1
    * Excluded some pure-technical part modules (like ModuleTestSubject and ModuleOverheatDisplay) from DIY kit complexity calculation, which decreases both SpecializedParts and SKH build costs of many kits. _**Note** for modders: the excluded part modules are listed in the IgnoreModules.cfg and could be added/changed using MM._

* v2.3 -- **Part Construction**
    * Compatible with KSP-1.7
    * Added "Add Part" option to **create kit from a single part**.

* v2.2 -- **In-place Construction**
    * Added **ability to construct kits inside assembly spaces**.
    * **Named docking ports** in Dockable Kit Container.
    	* _Only works with the newly created containers. Old ones will still have stock docking ports._
    * Added **display of additional resources** required for the vessel in a kit.
    	* Available in the interface of any workshop and via part menu of containers.
    * **Deployment Hint may be activated in Editor** (and in flight) via part menu of a container.
    * Deployment speed is inverse-proportional to the kit mass, so as not to brake everything with a too heavy deploing kit.
    * Spread parts along the **Tech Tree** more evenly:
        * Deployable Kit Container to Specialized Construction
        * Ground Assembly Line to Advanced Metalworks
        * Orbital Assembly Line/Space to Meta Materials

* v2.1 -- **Global Construction**
    * **Empty kit containers can be used as assembly spaces**
        * They can be created in Editor as well as inside some assembly spaces.
        * This completely lifts the limitation on the final mass and size of the vessel you construct.
    * **New DIY Kits can be created in orbit**
        * For that you need the new **Orbital Assembly Line**,
        * And the separate **Orbital Assembly Space**.
    * **Vessels can be constructed from DIY Kits in orbit**
        * For that you have to use a new type of kit container -- the **Orbital Kit Container**,
        * And the new **Orbital Workshop** part.
        * _Orbital Kit Container_ is limited in that it cannot store kits with launch clamps (for obvious reasons).
        * In an assembly line interface you can chose what type of the kit container to spawn or use for the kit you assemble.
    * Deploy hint is drawn as a 3D box rather than 2D "shadow" to accomodate orbital construction.

* v2.0.1.1
    * Added Machinery to kit resources whitelist

* v2.0.1
    * New ISRU patches by Critter79606
    * Several bugfixes

* v2.0.0 -- **Independence Day**
    * **!!! BACK UP YOUR SAVES !!!**
    * ***
    * **DIY Kits can now be created on other planets.**
        * To build a new kit you'll need the _**Ground Assembly Line**_ (a new part that you'll have to build from a DIY Kit brought from Kerbin) and a supply of _Material Kits_.
        * The _Assembly Line_ will first (painfully slow) convert _Material Kits_ to _**Specialized Parts**_.
        * Then you can select either a vessel from VAB/SPH, or a subassembly, and build a new DIY Kit using _Specialized Parts_, _Electric Charge_ and kerbal _engineers with Construction Skill_.
        * The new kit is then spawned inside the _Assembly Line_ and pushed out. At the top it has a place (marked by a small metal plate at the center) where any stock docking port can couple, so you can use it to grab the kit and transport it elsewhere.
        * Unlike kits produced on Kerbin (in Editor), off-world kits have NO recourses included. None at all. So, for example, if you build a nuclear reactor kit, on Kerbit it will include the radioactive fuel; but anywhere else you will have to find and transfer the fuel yourself.
    * **REMOVED workshop functionality from generic crewable parts.**
        * This means that all _non-GC/non-MKS_ parts will stop work as workshops. If a base relies on them, you need to build the Mobile Workshop there before installing GC2. Or you can copy MM patch from GC1 after the installation.
        * On the bright side, there's a new _**Inline Ground Workshop**_ part that has better efficiency than most of the generic workshops.
    * Fixed the **deployment of a kit in a time warp**; the kit doesn't jump into the air afterwards anymore.
    * Fixed the issue with determining the proper size of the stock RadialDrill.

* v1.3.0
    * Added on-demand rendering of spawn transform's forward direction
    * Fix for EL 6.0 by @LatiMacciato
    * Added DIY kit size constraints. Code greatly improved by **llinard**
    * Fixed KitRes display and Remaining Structure Mass calculation.
    * Fixed SpaceCrane RCS effects.

* v1.2.1.1
    * Using TextureCache to load icons.

* v1.2.1
    * Remove ISRU patch if USI is detected.
    * Fixed PartCost calculation.

* v1.2.0
    * **Removed OneTimeResourceConverter.** No need for it anymore.
    * Added **planet tabs** that group workshops located on the same celectial body to unclutter the workshop list.
    * Added ability to **synchronize construction target** among workshops belonging to the same vessel.
    * Added **Warp to end of construction** button.
    * Added **velocity dumping on vessel launch** to prevent bouncing and explosions of bulky ships and base segments.
    * Added separate checks and messages for vessel spawning to avoid confusion.
    * Scenario window is now shown after 3s after a level is loaded.
    * Fixed calculation of ETA of construction in the case of multiple workshops working concurrently on the same DIY Kit.
    * Fixed kit tremor after long time warp.
    * Changed symbol for Switch to workshop button.

* v1.1.2.2
    * Compatible with KSP-1.3
    * Engineers with lvl 0 are now also capable of kit construction.
    * Main GC Window now shows only landed workshops.
    * Hopefully fixed the issue with inability to launch some finished constructs.
    * Moved engineer patch to separate top-level config.
    * Fixed the coroutine bug.

* v1.1.2.1
    * Fixed double cost bug.
    * Fixed complexity and kit mass calculation.

* v1.1.2
    * Added custom part subcategory for GC workshops.
    * Added separate CKAN package for MKS bundle. Now GC is provided in two packages: GroundConstruction-Core and GroundConstruction (full). MKS only depends on the Core part.
    * Increased the **VolumePerKerbal** from 3 to **8 m3**. This effectively removes workshop capability from small cockpits, leaving it only in parts like Cupola or Science Lab.
    * Fixed the "cannot construct while moving" issue. Fixed zombie kits under construction.
    * Fixed vessel name loss in GC UI after undocking/decoupling.
    * Moved to the new DIY Kit model made by @BobPalmer.

* v1.1.1
    * **DIY Kit renaming** in editor and in flight.
    * **Improved User Interface**
        * DIY Kits are higlighted when mouse is hovered over their respective infos in Construction Window.
        * Efficiency and available Workforce are displayed at the top of the Construction Window and in the tooltips of the workshop fields in the Workshop List.
        * Workshop List now groups workshops by vessel and sorts them by planet and alphabetically.
        * Planet and Vessel fields in the Workshop List when pressed focus the camera on the corresponding planet/vessel in Map View or Tracking Station.
        * Workshop fileds of the active vessel when pressed toggle respective Construction Windows.
    * Added another 3s delay before fixing Kit to the ground.
    * **For modders**: added check for non-existing MODULE[NotGroundWorkshop] to blacklist parts to which GroundWorkshop module should not be added by MM.

* v1.1.0
    * **!!! Converted everything to use MaterialKits instead of Metals !!!**
    * * Added MM patch to let the truck also work for Extraplanetary Launchpads assembly (made by **Kerbas-ad-astra**).
    * Added the new ExperienceEffect: **ConstructionSkill** to use instead of the stock ones.
    * Added **OneTimeResourceConverter** to switch to new StructureResource.
    * Stop time-warp if the construction is suspended for some reason.
    * Before deployment the kit now checks for movement and ground contact. Then waits additional 3 seconds. This fixes the floating-kit bug. Also, the deployment time is now limited to prevent "explosive" deployment of very small kits.
    * Fixed the bug that caused a Kit to fall through the ground on Time Warp.
    * Made full ConfigurableContainers a dependency, othewrise GC provides no means of storing StructureResource.
    * A DIY Kit now stores another DIY Kit as is, without any mass/cost reduction (no more matryoshka cheating).
    * Fixed SEGFAULT on switching to an unloaded workshop from Flight.
    * Various small bugfixes.

* v1.0.0.1
    * Added **Kit Res.** part menu field that displays the amount of structural resource needed to assemble the Kit.
    * Changed skill required for Ore Smelters in IRSUs to match that of the stock IRSU converters.
    * Small bugfixes.

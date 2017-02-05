#Ground Construction ChangeLog

* **v1.1.1**
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
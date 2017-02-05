//   AssemblyInfo.cs
//
//  Author:
//       Allis Tauri <allista@gmail.com>
//
//  Copyright (c) 2016 Allis Tauri

using System;
using System.Reflection;
using AT_Utils;

// Information about this assembly is defined by the following attributes.
// Change them to the values specific to your project.

[assembly: AssemblyTitle("GroundConstruction")]
[assembly: AssemblyDescription("Plugin for the Kerbal Space Program")]
[assembly: AssemblyConfiguration("")]
[assembly: AssemblyCompany("")]
[assembly: AssemblyProduct("")]
[assembly: AssemblyCopyright("Allis Tauri")]
[assembly: AssemblyTrademark("")]
[assembly: AssemblyCulture("")]

// The assembly version has the format "{Major}.{Minor}.{Build}.{Revision}".
// The form "{Major}.{Minor}.*" will automatically update the build and revision,
// and "{Major}.{Minor}.{Build}.*" will update just the revision.

#if NIGHTBUILD
[assembly: AssemblyVersion("1.1.*")]
#else
[assembly: AssemblyVersion("1.1.1.0")]
#endif
[assembly: KSPAssembly("GroundConstruction", 1, 1)]

// The following attributes are used to specify the signing key for the assembly,
// if desired. See the Mono documentation for more information about signing.

//[assembly: AssemblyDelaySign(false)]
//[assembly: AssemblyKeyFile("")]

namespace GroundConstruction
{
	public class ModInfo : KSP_AVC_Info
	{
		public ModInfo()
		{
			MinKSPVersion = new Version(1,2,2);
			MaxKSPVersion = new Version(1,2,2);

			VersionURL   = "https://raw.githubusercontent.com/allista/GroundConstruction/master/GameData/GroundConstruction/GroundConstruction.version";
			UpgradeURL   = "https://github.com/allista/GroundConstruction/releases";
			ChangeLogURL = "https://github.com/allista/GroundConstruction/blob/master/ChangeLog.md";
		}
	}
}
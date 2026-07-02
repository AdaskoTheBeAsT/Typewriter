 From repo root:

   powershell
     .\rider\gradlew.bat -p rider verifyPluginProjectConfiguration
     .\rider\gradlew.bat -p rider verifyPlugin
     .\rider\gradlew.bat -p rider runIde

   runIde should launch a Rider sandbox with the plugin installed. Open a solution containing .tst files, then test:

   •  .tst file recognition and highlighting
   •  Tools > Typewriter > Generate Current Template
   •  Tools > Typewriter > Generate All Templates
   •  Tools > Typewriter > Validate Current Template
   •  Settings > Typewriter options
   •  save-time generation/validation

   To test packaged install:

   powershell
     .\rider\gradlew.bat -p rider buildPlugin

   Then install rider\build\distributions\typewriter-rider-<version>.zip in Rider via Settings > Plugins > Gear > Install Plugin from
    Disk.

    .\Build-RiderPluginWithTools.ps1 -Configuration Release

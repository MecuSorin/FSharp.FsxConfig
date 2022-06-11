#r "bin/Release/net6.0/FSharp.FsxConfig.Tests.dll"

let changeConfigurationX (conf: Program.MyConfiguration) =
    // you can have comments, the tooling will let you know if the typing is out of synch
    conf.something


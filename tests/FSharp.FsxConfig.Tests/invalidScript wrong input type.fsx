#r "bin/Release/net6.0/FSharp.FsxConfig.Tests.dll"

let changeConfiguration (something: string) =
    // you can have comments, the tooling will let you know if the typing is out of synch
    { Program.MyConfiguration with something = something }


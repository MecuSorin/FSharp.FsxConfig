# FSharp.FsxConfig

A simple tool that allows using fsx files for configuration.

### Note
There is a convention in place for the name of the function that is changing the config in the script file: `changeConfiguration`

### Limitations
Doesn't support saving a new configuration at runtime.

## Usage example
You can watch the example in tests\FSharp.FsxConfig.Tests or follow the description below:

```bash
dotnet new console -lang F#
dotnet add package FSharp.FsxConfig
```


Change the content of the program.fs with:
```fsharp
open FSharp.FsxConfig

type MyConfiguration = {
    athing: int
    something: string
}
module MyConfiguration = 
    let internal Default: MyConfiguration = { athing = 1; something = "default value" }

MyConfiguration.Default
|> applyScriptConfiguration "change app configuration.fsx"
|> Result.bind (applyScriptConfiguration "config.fsx")
|> printfn "Hello from F# %A"    
```

Create some 2 script files (check names from above example, and replace your project build output in the #r include):
```fsharp
#r "bin/Debug/net6.0/REPLACE_ME_WITH_YOUR_PROJECT_NAME.dll"

let changeConfiguration (conf: Program.MyConfiguration) =
    // you can have comments, the tooling will let you know if the typing is out of synch
    { conf with athing = conf.athing + 10 }
```	

```bash
dotnet run
```

### Troubleshooting

- something is wrong with files location, references, etc. you can see the details of errors in console by enabling verbose messages: ```FSharp.FsxConfig.verbose <- true```
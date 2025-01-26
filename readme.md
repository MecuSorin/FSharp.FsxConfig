# FSharp.FsxConfig

A simple tool that allows using `fsx` files for configuration. You can have comments, the tooling will support you with type checking. You can have complex types that can even load data at runtime from dynamic sources.

### Note
There is a convention in place for the name of the function that is changing the config in the script file: `changeConfiguration`

### Limitations
1. Configuration script name should translate in a valid namespace( so avoid funy file names, keep them simple).
2. Doesn't support saving a new configuration at runtime, but there are workarounds. One approach is to use the compiler to generate the code, another one is just to resolve the problem at hand like in this example: Given the context of a ML project that needed some prediction configuration to be saved/reused, my approach was to use this simple code to generate my config at training time, later to load the config from the application that was doing the predictions
```fsharp
type PredictConfiguration = {
    sequenceLength: int
    inputSize: int64
    outputSize: int64
    hiddenSize: int
    numLayers: int
}

module PredictConfiguration =
    let Default = { sequenceLength = 32; inputSize = 4L; outputSize = 6L; hiddenSize = 1024; numLayers = 2 }
    let formatPredictConfiguration (config: PredictConfiguration) =
        (sprintf "%A" config).Replace("{", "").Replace("}", "").Split('\n') |> Array.map(fun v -> sprintf "%s%s" (String.replicate 17 " ") (v.Trim())) |> String.concat "\n" |> sprintf "\n%s"
    let saveConfiguration executablePath configFilePath (config: PredictConfiguration) = 
        let text = 
            sprintf 
                """
#r @"%s"

let changeConfiguration (conf: Program.PredictConfiguration) =
    { conf with %s }
"""             executablePath 
                (formatPredictConfiguration config)
        System.IO.File.WriteAllText(configFilePath, text)
```
Using `PredictConfiguration.saveConfiguration` generated the following configuration file:
```fsharp

#r @"..\bin\Debug\net8.0\Engine.exe"

let changeConfiguration (conf: Program.PredictConfiguration) =
    { conf with 
                 sequenceLength = 32
                 inputSize = 4L
                 outputSize = 6L
                 hiddenSize = 1024
                 numLayers = 2 }
```
---

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

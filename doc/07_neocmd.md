# NeoCmd

## Usage

NeoCmd is a console application that is able to executes scripts.

Type in the lua code that you want to execute. If you let one line blank, that code will be executed.

Start the line with a colon to execute a command, like `:q' for exit.

## Commands

| Command    | Description |
|------------|-------------|
| `:q` | Exit the application. |
| `:h` | Show the command list. |
| `:list` | Lists all global variables. |
| `:load` | Loads the lua-script from a file. |
| `:debugoff` | Tell the compiler to emit no debug informations. |
| `:debugon` | Let the compiler emit debug informations. |
| `:debugtracLet` | the compiler emit trace line functionality. |
| `:c` | Clears the current script buffer. |
| `:env` | Create a fresh environment. |
| `:cache` | Shows the content of the binder cache. |

## Command line

It is possible to load a script during start up.

```
NeoCmd.exe [-debugoff | -debugon | -debugtrace ] [Scriptfile] ...
```
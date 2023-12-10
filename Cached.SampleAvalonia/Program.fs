open System

open Avalonia
open Avalonia.Controls
open Avalonia.Controls.ApplicationLifetimes
open Avalonia.Media
open Avalonia.Themes.Fluent

open Cached

type MainWindow() as me =
    inherit Window()

    let refresh =
        let s = createEmptyStorage ()

        // Protects from recursive calls of `refresh`.
        // This can happen when `refresh` changes property of existing component (eg. `IsChecked`)
        // and this change raises event whose handler (eg. `IsCheckedChanged`) calls `refresh`.
        let mutable refreshing = false

        let rec refresh () =
            if not refreshing then
                refreshing <- true
                let panel = runWithStorage s (TodoList.view refresh)
                if me.Content = null then
                    // Setting content once is good enough because returned `panel` is always
                    // the same instance in this program.
                    me.Content <- panel
                refreshing <- false
        refresh

    do
        me.Title <- "TODOs"
        me.Background <- Brushes.DarkSlateBlue
        refresh ()

type App() =
    inherit Application()

    override me.Initialize() =
        me.Styles.Add(FluentTheme())
        me.RequestedThemeVariant <- Styling.ThemeVariant.Dark

    override me.OnFrameworkInitializationCompleted() =
        match me.ApplicationLifetime with
        | :? IClassicDesktopStyleApplicationLifetime as desktop ->
            desktop.MainWindow <- MainWindow()
        | _ -> ()

        base.OnFrameworkInitializationCompleted()

[<CompiledName "BuildAvaloniaApp">]
let buildAvaloniaApp () =
    AppBuilder.Configure<App>()
        .UsePlatformDetect()

[<EntryPoint>]
let main argv = buildAvaloniaApp().StartWithClassicDesktopLifetime(argv)

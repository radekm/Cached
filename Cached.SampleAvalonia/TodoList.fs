module TodoList

open System
open System.Linq

open Avalonia
open Avalonia.Controls
open Avalonia.Layout
open Avalonia.Media

open AvaloniaHelpers
open Cached

type TodoItem = { Name : string; mutable Done: bool }
type TodoList = TodoItem ResizeArray

// `refresh` function ensures that the view is refreshed.
// Properties which don't change are initialized inside `cachedHere`.
// Properties which do change are initialized and then updated in `computation`.
let view refresh = computation "todo-list" {
    let! todoList = cachedHere {
        [
            { Name = "Write documentation"; Done = false }
            { Name = "Wrap this list in ScrollViewer"; Done = false }
            { Name = "Get some sleep"; Done = true }
            { Name = "Stop copying samples from Vide"; Done = false }
            { Name = "Make Cached more ergonomic to use"; Done = false }
        ].ToList()
    }

    let! itemsStack = cachedHere { StackPanel(Orientation = Orientation.Vertical, Margin = Thickness(4)) }

    let! newItemName = cachedHere {
        let tb = TextBox()
        tb.TextChanged.AddHandler(fun _ args ->
            args.Handled <- true
            refresh ())
        tb
    }
    let! newItemButton = cachedHere {
        let b = Button(Content = TextBlock(Text = "Add Item"))
        b.Click.AddHandler(fun _ args ->
            todoList.Add { Name = newItemName.Text; Done = false }
            newItemName.Text <- ""
            args.Handled <- true
            refresh ())
        b
    }

    let! mainPanel = cachedHere {
        let mainPanel = DockPanel()

        let h1 = h1 "TODO List translated from Vide"
        h1.HorizontalAlignment <- HorizontalAlignment.Center
        DockPanel.SetDock(h1, Dock.Top)
        mainPanel.Children.Add h1

        let newItemPanel = DockPanel(Margin = Thickness(4))
        DockPanel.SetDock(newItemButton, Dock.Right)
        newItemPanel.Children.Add newItemButton
        newItemPanel.Children.Add newItemName
        DockPanel.SetDock(newItemPanel, Dock.Bottom)
        mainPanel.Children.Add newItemPanel

        mainPanel.Children.Add itemsStack

        mainPanel
    }

    newItemButton.IsEnabled <- newItemName.Text |> String.IsNullOrWhiteSpace |> not

    use itemsStackChildren = refreshChildren itemsStack
    for i = 0 to todoList.Count - 1 do
        let! isDone = cachedHereUnder i {
            let cb = CheckBox()
            cb.IsCheckedChanged.AddHandler(fun _ args ->
                todoList[i].Done <- cb.IsChecked.Value
                args.Handled <- true
                refresh ())
            cb
        }
        let! name = cachedHereUnder i {
            TextBlock(VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis)
        }
        // Button which removes `i`-th item.
        let! removeButton = cachedHereUnder i {
            let b = Button(Content = TextBlock(Text = "Remove"))
            b.Click.AddHandler(fun _ args ->
                todoList.RemoveAt i
                args.Handled <- true
                refresh ())
            DockPanel.SetDock(b, Dock.Right)
            b
        }

        let! panel = cachedHereUnder i {
            let panel = DockPanel()

            panel.Children.Add removeButton
            panel.Children.Add isDone
            panel.Children.Add name

            panel
        }
        itemsStackChildren.Add panel

        let item = todoList[i]
        isDone.IsChecked <- item.Done
        name.Text <- item.Name
        removeButton.IsEnabled <- item.Done

    return mainPanel
}

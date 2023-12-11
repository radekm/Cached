module AvaloniaHelpers

open System

open Avalonia
open Avalonia.Controls
open Avalonia.Media

let h1 text =
    TextBlock(Margin = Thickness(0, 12, 0, 18), FontSize = 28, FontWeight = FontWeight.Bold, Text = text)

/// Helps to refresh the child controls of the panel `p` without first clearing them and then re-adding them.
/// Avoid manipulating `p.Children` manually while using `ChildrenRefresher`.
type ChildrenRefresher(p : Panel) =
    let mutable disposed = false
    let mutable i = 0  // Number of refreshed children.

    // TODO: What if `item` has different parent or is in `p` but at higher index `i`?
    //       We shall remove `item` from its current parent.
    member _.Add(item : Control) =
        if disposed then
            failwith $"%s{nameof ChildrenRefresher} has been disposed"
        if i < p.Children.Count then
            if not (LanguagePrimitives.PhysicalEquality p.Children[i] item) then
                p.Children[i] <- item
        else
            p.Children.Add item

        i <- i + 1

    interface IDisposable with
        override _.Dispose() =
            if p.Children.Count > i then
                p.Children.RemoveRange(i, p.Children.Count - i)
            disposed <- true
            // We don't set `i` to zero because `ChildrenRefresher` instance shall not be reused.

let refreshChildren (p : Panel) = new ChildrenRefresher(p)

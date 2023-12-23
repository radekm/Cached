# Computations with cached values

Cached library implements F# computation expressions where values can be cached
and on the next run cached values can be reused instead of recomputed.

The main use for this library is to create and update user interfaces.
Having separate code for creation and update of user interface leads to inconsistencies.
To remedy this we can avoid updates and always recreate user interface from scratch
while preserving it's internal state. And this is what Cached library helps with.
You can write a single computation expression which can be used to create and also
to update a user interface. Caching controls with internal state ensures
that internal state is preserved and also improves performance.

## Motivation

My goal was to create UI in F#. But unfortunately there are no
production-ready reactive libraries for F# and not even for .NET.
That's at least my opinion and I'll explain why.

Currently the closest candidates are

- `ImGui.NET`. Dear ImGui is an awesome immediate mode UI library.
  `ImGui.NET` NuGet package works with Silk.NET rendering.
  Unfortunately it doesn't look and behave as other desktop applications.
- `egui` written in Rust. It is also an immediate mode library which looks and behaves
  more like other desktop applications (at least on Mac). 
  I tried wrapping `egui` so I can use it from F#.
  Unfortunately wrapping Rust code for other languages needs unsafe code
  and thus it's harder than wrapping C or C++ code.
  The reason is that Rust compiler expects that certain aliasing rules are followed.
  These aliasing rules are more complex than for C and C++ and can be easily violated
  in unsafe code thus introducing undefined behaviors.
- `Avalonia`. This library looks and behaves fine on various platforms.
  Unfortunately I don't like XAML nor its reactive part.

The other well known libraries are either full of bugs like MAUI.
Or are extremely complex like React or Blazor or Jetpack Compose or even Solid.js.
These widely used libraries are maybe based on simple ideas
but their implementation is complex
(all have more than 1 K lines and some even more than 10 K lines).
This complexity hides special cases which will probably surprise you.
And I don't want to be surprised by my applications in production environments.

I also tried other F# libraries which were adding reactivity to Avalonia.
But most of them were not working properly even in simple scenarios.
Among F# libraries I really liked [Vide](https://github.com/vide-collabo/Vide).
Cached library is inspired by Vide.
The main difference is that a type of a state used by a computation expression
is not propagated outside. This makes resulting types much friendlier to the user
and probably to the type checker. On the other hand types of Vide computation expressions
contain a type of a whole state which can be massive.

Cached library tries to be simple. Currently it has less than 300 lines.
so anyone can read its source code and understand everything.
Cached library can be used to add reactivity
to retained mode UI libraries like Avalonia.

## Tutorial — Basics

Let's construct our first computation with cached values:

```fsharp
open Cached

let c = computation "counter" {
    let! n = cachedHere { ref 0 }
    n.Value <- n.Value + 1
    printfn "Count is %d" n.Value
}
```

We gave our computation a name `"counter"` and stored it in a variable `c`.
To run the computation we need a storage:

```fsharp
let storage = createEmptyStorage ()
```

When we run our computation for the first time with

```fsharp
runWithStorage storage c
```

it prints `Count is 1`. When we run it with the same storage for the second time
it prints `Count is 2`. And you can bet that when we run our computation for the third time
and use the same storage it prints `Count is 3`.

`n` in the computation has type `int ref`. When the computation is run
`cachedHere` looks into the storage to see if there is a value cached by a previous run.
If there is no value from a previous run then `cachedHere` evaluates the expression in the braces
to create a value, puts the value into the storage and also returns the value.
If there is already a value in the storage then `cachedHere` returns this value.

In our example in the first run `cachedHere` evaluates `ref 0`, puts the result into the storage
and also returns the result. So in the first run `n` is bound to `ref 0`.
In the next runs `cachedHere` finds the previously cached value in the storage and returns it.
This means that subsequent runs of our computation `c` use mutable cell
which was created by the first run.

`cachedHere` uses the line number of the last `let!` as a key into the storage. So multiple calls of `cachedHere`
work fine as long as `let!`s are on different lines

```fsharp
let c2 = computation "date and counter" {
    let! date = cachedHere { DateTimeOffset.UtcNow }
    let! n = cachedHere { ref 0 }
    n.Value <- n.Value + 1
    printfn "Count is %d, counting started at %A" n.Value date
}
```

If we evaluate a single `let!` multiple times

```fsharp
let c3 = computation "multiple dates" {
    for i = 1 to 5 do
        let! date = cachedHere { DateTimeOffset.UtcNow }
        printfn "%d-th iteration executed at %A (in the first run)" i date
}
```

we get an exception saying `Value at ... cannot be used twice`.
The reason is that usually when caching inside a loop we want
different iterations of the loop to not interfere with each other.
In other words we usually don't want value cached in the first iteration
to be used by the second iteration.

So using the line number of the last `let!` as the key into the storage is not enough
in the above example. Fortunately there's a function `cachedHereUnder`
which uses the line number of the last `let!` and the value given by the user as the key into the storage:

```fsharp
let c4 = computation "multiple dates" {
    for i = 1 to 5 do
        let! date = cachedHereUnder i { DateTimeOffset.UtcNow }
        printfn "%d-th iteration executed at %A (in the first run)" i date
}
```

This basic knowledge is enough to build simple user interfaces.
See `Cached.SampleAvalonia/TodoList.fs` as an example.

### Using line number of `let!` vs line number of `cachedHere` 

As explained above `cachedHere` and `cachedHereUnder` use the line number of the last `let!`
as the key into the storage. The consequence is that both calls of `cachedHere` in
the following example use the same key into the storage

```fsharp
let c5 b = computation "let! matters" {
    let! x =
        if b
        then cachedHere { 1 }
        else cachedHere { 2 }
    return x
}
```

which may result in a surprising behavior. The result of `c5` computation will
depend on the value of `b` given to the first run.

Another alternative for `Cached` library would be to use a line number of `cachedHere` or `cachedHereUnder`.
This alternative would make it harder to use `cachedHere` and `cachedHereUnder` outside of computations.
The following example would fail

```fsharp
let cachedInstance = cachedHere { obj() }

let c = computation "two different instances" {
    let! x = cachedInstance
    let! y = cachedInstance
    return x, y
}
```

with an exception saying `Value at ... cannot be used twice`.
Currently when using the line number of the last `let!` it works fine.

### Eviction of unused values 

Each call `runWithStorage storage c` monitors which values in a storage `storage`
are used by a computation `c`. After `c` finishes values which were not used by `c`
are removed from `storage`.

## Tutorial — Scopes

In the previous tutorial when defining computations we used expressions of the form
`computation computation-name { ... }` and always gave our computations a name.
The name can be any value whose type satisfies `equality` constraint.

Computation names determine scopes. Scopes exist to ensure that different computations
don't interfere with each other. For example suppose that we define

```fsharp
let counter = computation "counter" {
    let! n = cachedHere { ref 0 }
    return n
}

let countPositive xs = computation "countPositive" {
    let! n = counter
    xs
    |> List.iter (fun x -> if x > 0 then n.Value <- n.Value + 1)
    return n.Value
} 

let countNegative xs = computation "countNegative" {
    let! n = counter
    xs
    |> List.iter (fun x -> if x < 0 then n.Value <- n.Value + 1)
    return n.Value
}

let updateAndPrintStats xs = computation "updateAndPrintStats" {
    let! positive = countPositive xs
    let! negative = countNegative xs
    printfn "We have seen %d positive numbers and %d negative numbers" positive negative
} 
```

In the above example `counter` computation is used by both `countPositive` and `countNegative` computations
and yet when we run `updateAndPrintStats` we see that two counters don't interfere with each other:

```fsharp
let countingStorage = createEmptyStorage ()

runWithStorage countingStorage (updateAndPrintStats [-1; 1])
// Prints: We have seen 1 positive numbers and 1 negative numbers

runWithStorage countingStorage (updateAndPrintStats [-2; -3])
// Prints: We have seen 1 positive numbers and 3 negative numbers

runWithStorage countingStorage (updateAndPrintStats [-4; 2; 3])
// Prints: We have seen 3 positive numbers and 4 negative numbers
```

The reason is that `cachedHere` is called from different scopes
and different scopes don't share any data in the storage.

- If `counter` is used from `countPositive` then `cachedHere` is called
  from the scope `"updateAndPrintStats"` / `"countPositive"`  / `"counter"`.
- On the other hand if `counter` is used from `countNegative` then `cachedHere` is called
  from the scope `"updateAndPrintStats"` / `"countNegative"`  / `"counter"`.

A scope is something like a call stack for computations. Initially
`runWithStorage storage root` opens the scope for `root` computation.
If `root` computation calls `nested` computation then
it opens the scope for `nested` computation. When `nested` terminates
the scope for `nested` is closed. Finally the scope for `root` is closed.
`cachedHere` and `cachedHereUnder` operate only on the currently open scope.
This means they look into the storage only for values stored in the currently open scope
and when they put a new value into the storage they put it in the currently open scope.
This also means they don't see values stored in the parent scope.

Earlier we saw that we can't call `cachedHere` multiple times from the same line.
Similarly to that we can't open one scope multiple times.
Or in other words we can't open one scope after we closed it.
If we run the following computation

```fsharp
let twoCountersWrong = computation "twoCountersWrong" {
    let! m = counter
    let! n = counter
    printfn "Values are %d and %d" m.Value n.Value
}
```

we get an exception saying `Scope cannot be used twice: "counter"`.
When evaluating `let! m = counter` we open and close the scope `"twoCountersWrong"` / `"counter"`.
The evaluation of `let! n = counter` then fails because it tries to open the scope `"twoCountersWrong"` / `"counter"`
which was already closed.

If we really want to use `counter` twice we have to do it from different scopes. For example

```fsharp
let twoCounters = computation "twoCounters" {
    let! m = computation 1 { return! counter }
    let! n = computation 2 { return! counter }
    printfn "Values are %d and %d" m.Value n.Value
}
```

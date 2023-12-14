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

## Tutorial

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

`cachedHere` uses the current line number as a key into the storage. So multiple calls of `cachedHere`
work fine as long as they are on different lines

```fsharp
let c2 = computation "date and counter" {
    let! date = cachedHere { DateTimeOffset.UtcNow }
    let! n = cachedHere { ref 0 }
    n.Value <- n.Value + 1
    printfn "Count is %d, counting started at %A" n.Value date
}
```

If we call `cachedHere` multiple times from the same line

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
In other words we don't want value cached in the first iteration
to be used by the second iteration because otherwise we would call `cachedHere`
before the loop and not inside the loop.

So using the current line number as the key into the cache is not enough
in the above example. Fortunately there's a function `cachedHereUnder`
which uses the current line number and the value given by the user as the key into the cache:

```fsharp
let c4 = computation "multiple dates" {
    for i = 1 to 5 do
        let! date = cachedHereUnder i { DateTimeOffset.UtcNow }
        printfn "%d-th iteration executed at %A (in the first run)" i date
}
```

And that's all. This basic knowledge is enough to build simple user interfaces.
See `Cached.SampleAvalonia/TodoList.fs` as an example.

## Tutorial 2 - Scopes

TODO

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

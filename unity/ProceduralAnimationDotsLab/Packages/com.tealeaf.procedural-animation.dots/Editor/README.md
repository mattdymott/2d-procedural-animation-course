# Editor

The creature Bakers, validation, and derived Scene-view preview belong here.
The authoring components themselves are runtime-facing so designers can attach
them to a GameObject; this module serializes them into package-owned runtime
state.

`CreatureBakers.cs` holds one Baker per authoring component. Each bakes only its
own feature's data, and reads sibling *authoring* components (never another
Baker's output) when it needs shared facts like the chain's rest layout — which
is what keeps them independent of baking order.

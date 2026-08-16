Per-color mockup images for the Customize page (Views/Customize/custom.cshtml).

Path convention:
  /images/custom/{category}/{color}.png          <- main photo for that color
  /images/custom/{category}/{color}-2.png        <- optional extra showcase angle
  /images/custom/{category}/{color}-3.png        <- another extra angle, etc.

{category} is the folder key (e.g. cotton-tshirt, jacket-hoodie, tote-bag —
see Utils/CategoryImageResolver.cs for the full list and keyword matching).

{color} is the color name lowercased, spaces -> hyphens
  (e.g. "Dark Brown" -> dark-brown.png, dark-brown-2.png, ...)

Examples:
  /images/custom/cotton-tshirt/red.png       <- main "Red" photo
  /images/custom/cotton-tshirt/red-2.png     <- second angle of Red
  /images/custom/cotton-tshirt/red-3.png     <- third angle of Red
  /images/custom/jacket-hoodie/navy.png

You don't need every color filled in, and you don't need extra angles for
every color either — if a file is missing:
  - a missing {color}.png falls back to the plain category thumbnail
  - a missing {color}-2.png etc. just means that color's thumbnail strip
    only shows however many photos actually exist for it

Clicking a color swatch on the Customize page swaps BOTH the main image and
the whole thumbnail strip to that color's photos.

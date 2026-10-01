# Specials

- Manage offers at `/Majordome/Specials` (also `/Specials/Index`). Bloggers and other Majordome roles use the existing admin guard.
- Customers see published cards at `/Specials/List`. Each card links to `/Specials/Special/{id}`, using the same title and rich content.
- The card rich-text editor supports formatting and image insertion, dropping and pasting image files. There is no separate cover image or second page editor.
- Uploads accept PNG, JPEG, GIF and BMP up to 5 MB, with a 6,000-pixel side / 20-megapixel limit. Images are decoded and saved as PNG with generated names.

## Database and hosting

Run `Scripts/Sql/CreateSpecials.sql` against the target GTX database before releasing the feature. It creates `dbo.Specials` independently of Blogs and is safe to rerun. It has been applied to the configured development database only; production requires the same script when deploying. Databases created by the earlier version may retain unused cover/page columns; these are ignored to preserve existing data.

The application pool needs write access to `App_Data/SpecialsImages`. Preserve and back up this directory across releases; uploads are served through `/Specials/Image/{id}` and are excluded from source control. Unreferenced images from canceled edits are retained, so shared image links are not broken by deleting an offer.

## Checks

Build `GTX.csproj`, then run `powershell -NoProfile -ExecutionPolicy Bypass -File tests/specials.ps1`. Tests restrict SQL access to the configured local development instance and roll back their fixtures. They cover persistence, publication visibility, shared card/page content, sanitization, admin guards and anti-forgery attributes.

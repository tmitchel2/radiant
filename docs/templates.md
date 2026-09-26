# Radiant Templates

`Radiant.Templates` holds blocks: whole sections of an app (a page shell, a sign-in card, a
pricing table), built only from `Radiant.Components` and `Radiant.UI.Core`. Use them as they
are, or copy one into an app as a starting point, as with Tailwind UI or shadcn blocks. They take
their colours, shapes and type from the theme, so they follow a theme change like any component.

Every block is a `Component` record: data in as parameters, events out as callbacks, with no
state beyond what the block itself needs (a form's field values).

## Application UI

| Block | What it is |
|---|---|
| `SidebarLayout(title, items, selected, onSelect, content)` | The desktop app shell: a navigation drawer on the left, and a top app bar over the scrolling page. The bar takes its container colour once the page scrolls. Content is centred up to `MaxContentWidth`. |
| `PageHeading(title)` | A page's title, a line under it, and actions on the right. |
| `StatsGrid(stats)` / `Stat` | Key figures in cards, each change shown as a signed percentage, coloured success (up) or error (down). |
| `StackedList(entries)` / `ListEntry` | Rows with an avatar, a title and subtitle, a status chip and a meta line; `OnPress` gets the row's index. |
| `SettingsSection(title, rows)` / `SettingsRow(label, control)` | Settings grouped under a heading, with its description to the left and the rows in a card. |
| `EmptyState(icon, title)` | A centred icon, a title, a description and an action, for a view with nothing in it. |
| `SignInForm(onSignIn)` | Email and password (hidden, with a reveal button), remember me, a forgotten-password link and the sign-in button. Enter in the password submits. |

## Marketing

| Block | What it is |
|---|---|
| `Hero(headline)` | An eyebrow, a large headline, a paragraph and action buttons, beside an optional picture, on a primary container. It wraps under the picture when narrow. |
| `FeatureGrid(title, features)` / `Feature` | Features in up to three equal columns, each with a tinted icon. |
| `PricingTiers(tiers, onChoose)` / `PricingTier` | Plans side by side. A `Featured` plan is raised, filled and marked "Most popular". |

## Ecommerce

| Block | What it is |
|---|---|
| `ProductGrid(products, onAddToCart)` / `Product` | Product cards in up to four equal columns: picture (with a badge), name, detail, rating, price and an add-to-cart button. |
| `ProductCard(product)` | One of those cards. |
| `CartSummary(lines)` / `CartLine` | Each line with its picture, quantity buttons and amount, then subtotal, shipping (or "Free"), total and checkout. Quantity changes are reported, not applied: the app owns the cart. |

## Desktop shells

These are whole windows. The gallery shows each one in a framed preview.

| Block | What it is |
|---|---|
| `WorkspaceLayout(activities, activity, onActivity, editor)` | An IDE-style docked workspace: an activity bar choosing the side bar's content, the side bar, the editor with a panel under it and an inspector beside it, each behind a `Splitter`, and a status bar. Any part left null goes, with its splitter. |
| `MasterDetail(items, selected, onSelect, detail)` | A searchable list beside the chosen item's detail (mail, notes). Up and Down move through the list; searching keeps indices in the full list. With nothing chosen the detail shows an empty state. |
| `InspectorSection(title, rows)` / `PropertyRow(label, control)` | An inspector's collapsible property groups, names in a fixed column. |
| `Wizard(steps, current, onStep)` / `WizardStep` | A step-by-step flow: steps listed on the left (done ones ticked and revisitable), the current step's title and content, and Cancel, Back and Next or Finish. A step with `CanContinue = false` holds Next. |
| `PreferencesLayout(categories, selected, onSelect, content)` | A preferences window: a compact category list with section headings, and the chosen category's settings under its name. |

## Layout

Blocks that lay out repeated cards use `Grid` (see [ui.md](ui.md#grids)), so columns stay even
and a short last row lines up. A block given a `Layout` adds it to its own (see layered facets in
[ui.md](ui.md#style-facets)).

## Testing

`Radiant.Templates.Tests` mounts each block in a themed `UIRoot` and drives it through its
semantics tree: type into fields by label, press buttons by name, and read the text shown. The
gallery renders every block as a page, light and dark, to PNG (see
[components.md](components.md#the-gallery)).

## Still to come

- **More desktop shells:** onboarding, and a document editor with split editor groups.
- **More Application UI:** tables, description lists, calendars, feeds, command palette and
  notifications, as the P9 components (`DataTable`, `CommandPalette` and others) land.
- **More Marketing:** bento grid, testimonials, team, FAQ, logo cloud, newsletter, contact, footer
  and 404.
- **More Ecommerce:** product overview and quick view, category filters, checkout, order history
  and reviews.
- **Goldens:** each block at three widths × two densities × light and dark.

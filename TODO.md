# To-do Lists

## SharpSheets/SharpEditor

### Critical

- Documentation
    - Cards Tutorial
        - Definitions
        - Subjects
    - Card definition/subject types documentation


### Preferable

- Make use of ResourceDictionary to unify UI style
    - Create button style for toolbar buttons
    - Separate out application colours
    - Creating skin system for user-controlled colours
        - Bind brush colours to DependencyProperties
        - Create some kind of ItemsPresenter for this, to avoid overly verbose code

- The environment variables used when parsing certain MarkupDiv properties don't match those used when actually evaluating them
    - Could there be more explicit type management here? Some kind of MarkupLayoutEnvironment vs MarkupCanvasEnvironment types?
- Implement Shapes being able to create examples with pre-set example values (like Widget examples)
- CardSubject tooltip format
- Need some way to account for the fact in `IBox.FullRect` that the incoming rect may be zero in one axis (as it can be more an abstract representation of a size)
- Markup textRect doesn't allow for tspans to have different font-size values
- `Section` and `Box` widgets should be merged into one abstract superclass, with subclasses that only differ by whether or not the field area is multiline
- `SlotsBar` `labels` argument should probably be a 2-tuple of string
- Remove NotImplementedException throw statements from code (replace with NotSupportedException)
- Consider removing a lot of the ArgumentException throw statements, as they are not very expressive
- SharpEditor tries to find `source` directories in several places, with potentially dangerous results - check such cases.
- Make the MarkupExamplesDocument draw two versions of MarkupDetail objects (both Layouts)
- Allow Markup TitleStyles?
- It would be good to have an "Up" button in the documentation window
- The documentation page for IFramedContainerArea shapes should automatically determine and report if the full area can be inferred
- A color list in the documentation would be nice
    - Perhaps with a color picker and hex code, to view colors
- A font listing, and font pages, showing licensing and embedding options, available characters
    - And character sets?
- Need to fully test `Numbered<>`, especially with regard to Markup default and example arguments
- StyledDivElements should accept graphics state arguments (linewidth, colors, etc) so that box styles can be adjusted
- Need a way for a markup widget to know if it has children
- Create more specific `IEnvironment` types, to better distinguish uses in Markup/CardDefinitions
- Text field values are not displayed in the designer
- Numbered `<grouparg>`s would also be great
- There should be a way to paint text with gradients, etc., in Markup
    - Currently `text-color` can only be a solid color
- It would also be good to allow the notes/labels on `Bars`/`UsageBars` to be placed on any side of the bar (TOP, RIGHT, BOTTOM, LEFT) to allow for funky bar styles
    - This also opens the possibility of better using the `IEntriedArea` instead of specifically `IUsageBar`

- It would be great to clean up the environments, such that they are typed to better help distinguish
    - This might also help document them better?
    - Is it a good idea to have a base environment, that contains the "built-in" functions? (Then it can self-document too)


### Non-Critical

- Create a variable-numbered EntriedShape type, along with corresponding MarkupPattern
- The Markup code needs some refactoring. It's a mess.
- Double-check that UTF-8 encoding is used for all files when saving
    - Is the hard-coded UTF-8 for reading acceptable? Do we want to try and be more permissive? How?
- Consider changing `ParameterInfo.IsOptional` uses to `ParameterInfo.HasDefaultValue`, as the former is compiler dependent, but the latter is not (apparently)
- Fix CardTable spacing issue where the bottom cell extends beyond the edge of the area
- Need some way to indicate for a font collection path which font index is desired
- What's up with the `provides-remaining` logic in the markup now? Does that still do anything meaningful?
- What are `StyleSheet` `drawing-coords` actually doing? (End up in `MarkupGeometryState`)
    - Should we just remove `canvas` and `drawing-coords`?
    - How about slicing?
- Is nested constructor autocomplete functionality working yet? (e.g. `outline.title.format`)



### Possible Improvements

- Layers (in resulting PDF documents)
- Alternate card layout engine (scroll layout, as in 5e MM)
- CardSubject dictionary data? (How to format?)
- Change layout of card attributes
    - Attribute lines for CardSections?
    - "=" for card attribute values?
- Card details array values (How to format? Could be solved by separate attribute lines)
- Make font directory value more system agnostic
- Move `ITitleStyle` to its own type hierarchy, separate from `IShape`, and create a non-reflection-findable `IContainerShape` to hold a shape and titlestyle
    - This will simplify the `ShapeFactory` and `SharpFactory` methods, and make everything more straightforward
        - As the `ITitleStyle` objects will not need the "name" value when constructed
    - `Section` and `Box` can then assemble their own `IContainerShape` as needed
    - This will also make it just "title.style", rather than "outline.title.style", which is possibly more intuitive
    - Actually could offload the assembly to a special `ISharpArgs` class, which can even selectively ignore the title style
        - Although this won't affect "unused line" data
        - Maybe that's a good thing, as it makes untitled widgets more predictable (not dependent on the presence of another property)
        - Does this benefit from being handled specially by `SharpFactory`?
    - What if we wrap `string name` into a `TitleText` class that can pre-compute newline splits and so on?
    - Problem: TitledBox still requires the name on creation
        - Separate interface for ITitledBox that adds a new Draw(canvas, rect, name) method?
        - Then you check IBox/IContainer instances to see if they're ITitledBoxes before continuing?
            - You could then wrap the ITitledBoxes inside a non-reflection-findable class that also stores the name?
- Add "Name" and "Description" tags to the Card Definitions, Sections, and Features
    - These tags can then be used in the context menu to "Add <Tag> Section", to autofill the necessary information in Card Subject files
- Allow dictionary definitions of shapes? This could allow there to be arrays of shapes
    - How to deal with overriding the choice of shape?
        - When we have "outline: {style: ...}", how do we know to pick the closer "outline.style: ..." in a deeper context?
        - Do we allow the contexts to be used together? Can we later override specific parameters?
        - How do we integrate these sub-contexts into the overall context?
            - Do we have to include them in the initial parse? How to distinguish them from children, then?
        - What if we simply expand all such dictionary style expressions into separate context properties?
            - `outline: {style: A, trim: B}` becomes `outline.style: A` and `outline.trim: B`
            - Would require a rewrite of some other components of the parsing pipeline (Margins and Positions, for example)
            - But it does make the whole system more robust, and exploitable for other uses
            - How do we cope with arrays of these dictionary style objects?
                - Just don't expand them, and treat these as separate entities to be parsed?
- Can `FullRect(ISharpGraphicsState, Rectangle)` be converted to just `FullSize(ISharpGraphicsState, Size)`?
    - Looks like all the downstream tasks only care about the overall size, rather than the positioning?
        - Other than the MarkupExamplesDocument, but that's not important
    - Problem: Some TitleStyle FullRect calculations rely on the relative positioning of Rectangles
        - Can this be faked by some other mechanism?
        - What if `FullRect` actually returned a `Margins` object? Then the positioned `Rectangle` can be calculated, or a `Size` adjusted, as needed?
        - What exactly is the benefit of doing this now?
- There is currently no way to provide example values for built-in shape arguments (to be displayed in the documentation window)
- Should the patterns in Markup files have a special "root" node, instead of using the outer "pattern" node as root?
    - Might make things a little cleaner, as currently things like "gutter" have to be put on the pattern, which doesn't make much sense
- Can we give arguments to card definition macros in card subject files?
    - e.g. `#= MyCardDefinition [arg1: value1, arg2: value2]`
    - Would need a way of distinguishing card arguments (`def name|...`) from definition arguments
        - `var name|...: int = ...` maybe?
     - All of these arguments would need default values (or an error when a required argument is missing?)
- Should registries by locked somehow while changing template directory?
- Improve text handling in SharpSheets (specifically from ISharpCanvas)
    - Use something more akin to "BeginText"/"EndText" pattern from PDF?
- Better SVG integration for Markup (for copy-pasting)
    - Have a flag to use screen-space (top-left) rather than page-space (bottom-left) coordinates
- Slicing in markup needs cleaning up
    - Have a way of scaling the "slicing" operation, such that a fixed-width becomes variable-width
    - Absence of "canvas" for pattern needs to cause error for "slicing" XML elements
- Can the `&SOURCE` syntax be improved?
    - Could there be a way for paths to reference some default location (i.e. `&TEMPLATE`), so that we can more easily reference template resources?
- Could do with improving template file menu (subdivide into separate submenus?)
- It would be good to get a list of fonts in the help window
    - With a print out of any licence that the font may have (along with the provided embedding flags)
- What if we could have z-indexing for drawing operations?
    - For PDF, this could be faked using XObjects for each listed z-index, which can then just be printed in z-order
    - Would need a way of inserting XObjects into the GraphicsStream (which would require editing the GraphicsStream, or "closing" the ISharpCanvas so they can be drawn at the end)
    - This would mean that single widgets/shapes could draw "background" information that didn't overdraw neighbours
- Switch to AvaloniaUI and AvaloniaEdit?
- What if, instead of using `float` values, we have a `Length` type that could be used for distances?
    - It could accept values with different units, but default to `pt`?
- Some of the `EvaluationNode.ReturnType` calculations could return a tuple rather than an array
    - Is this actually useful anywhere, or is it just cool?
    - Indexing into a tuple can actually check constant-indexer operations to determine if the indexing is valid or not
- Can we add `<data>` child entities to `<path>` entities? (So we can use `enabled` with them)
- Need to properly implement fill patterns in PDF and Designer, for use with Markup
    - This will probably require some kind of sub-canvas on which to draw the pattern unit
- Need to directly check characters requested from font, to catch character codes with no matching glyph
    - Currently the designer may display backup characters that PDF does not have access to
    - Need to check characters and provide warning, and possibly replace character code with "unknown character" code
- It might be good to be able to specify config properties for specific widget types
    - i.e. `section.field.format: BOLD`
    - In this case should we avoid having fields with the same names as widgets?
- Should `ShapeFactory` actually return a `null` if no `style` value is provided?
    - Currently there is no way for a widget to actually provide a custom "default" value for a shape, as it will always be overriden by ShapeFactory
- Could child divs be inheritable?
    - This would necessitate a rewrite of the Named Child code
        - Currently we rely on the named children being identifiable
        - What if we just store all children the same, and then check if the child has a valid name when building objects?
    - What do we do for named children at the top level?
        - Will the strategy of just ignoring children that don't have recognized names work?
    - (Now also need to update the `Numbered<>` handling as well)
- Pattern library names could use the relative path inside the template directory, not just the filename, if no name is specified
    - Can we do this? SharpSheets does not know about template directories (that's an editor thing)
- Can we do a Title Styled Box version of the `StyledDiv`?
    - Would then need adding to the `MarkupDocumentation` listings, and other places


## GeboPDF

- Implement font subsetting
- Try creating `/Encoding`/`/ToUnicode` CMap pairs for use representing all characters in a font, while mapping perfectly to Unicode
    - This will involve using a Unicode private region for storing alternative characters
        - Should use one of the larger regions, as they allow for basically no chance of running out of space (same size as max numGlyphs in TrueType)
- Increase coverage of specification:
    - Include more document catalogue dictionary entries
    - Include more `Page` (leaf nodes of pages tree) dictionary entries
- Text fields don't currently support default values
- Implement graphics state checking to avoid unnecessary graphics state operators in graphics stream
    - Is there any way to apply this to the SaveState and RestoreState functionality?
- Cleanup color spaces and patterns (especially Tiling Patterns)
    - Enforce Uncolored Tiling Pattern restrictions on graphics operators
- Transform tracking in `GraphicsStream`?
- Auto calculation and calculation order
- Layers ("optional content")
- Can image handling be improved?
- Cleanup AFM file handling (it's never supposed to be visible, but it's very ugly)
- Move `Equals` methods into static methods, to better separate logic - let leaf subclasses overload Equals if they think they know how
    - Would it be better to implement an `IEqualityComparer<PdfObject>`?
        - Perhap relying on making a bunch of the PdfObject subclasses `IEquatable<>`?
- Cleanup `PdfStreamReader` (the overlap between static and non-static methods is very confusing)
- Is it possible to provide `TextRenderingMode` or fill opacity values to editable form fields?

- Extend `GraphicsStream` to make `PageGraphicsStream`, which can be used to create structured content sequences
    - This `PageGraphicsStream` holds a reference to the `Page`, which can then be used as an intermediary with the `PdfStructTree` dictionary
    - Using this, we can create marked content sequences which are automatically fed back to the structure heirarchy
    - (Just get this working for paragraphs, and then worry about nested structure later, if necessary)

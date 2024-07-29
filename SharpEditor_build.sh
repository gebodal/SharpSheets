#!/bin/bash

# Variables
PROJECT_NAME="SharpEditor"
PROJECT_PATH="./$PROJECT_NAME/$PROJECT_NAME.csproj"
OUTPUT_DIR="./published"
VERSION=$(grep -oP '(?<=<Version>)[^<]+' $PROJECT_PATH)

if [ -z "$VERSION" ]; then
    echo "Version not found in $PROJECT_PATH"
    exit 1
else
	echo "Building $PROJECT_NAME version $VERSION"
fi

if ! [ -x "$(command -v dotnet)" ]; then
	echo 'Error: dotnet is not installed.' >&2
	exit 1
fi
if ! [ -x "$(command -v zip)" ]; then
  echo 'Error: zip is not installed.' >&2
  exit 1
fi


RUNTIME_IDS=(
    "win-x64"
    "win-x86"
    #"win-arm"
    "osx-x64"
    #"osx-arm64"
    "linux-x64"
    #"linux-arm"
    #"linux-arm64"
    #"linux-musl-x64"
)

# Create output directory
echo "Make directory $OUTPUT_DIR"
mkdir -p "$OUTPUT_DIR"
echo

# Build and zip for each runtime
for PLATFORM in ${RUNTIME_IDS[@]}; do
    PLATFORM_OUTPUT_DIR="$OUTPUT_DIR/$PLATFORM"

    echo "Building for $PLATFORM..."

	echo "Make platform directory $PLATFORM_OUTPUT_DIR"
	mkdir -p "$PLATFORM_OUTPUT_DIR"

    dotnet restore "$PROJECT_PATH"
    # IsPublishing is a custom flag to set OutputType to WinExe
    dotnet publish "$PROJECT_PATH" -c Release -r "$PLATFORM" --self-contained -o "$PLATFORM_OUTPUT_DIR" -p:PublishSingleFile=True -p:PublishReadyToRun=True -p:IsPublishing=true

	echo "Removing build artifacts from $PLATFORM_OUTPUT_DIR"
	rm "$PLATFORM_OUTPUT_DIR/SharpSheets.xml" # Better way of doing this?

    echo "Zipping $PLATFORM output..."
    zip -r "$OUTPUT_DIR/${PROJECT_NAME}-$VERSION-$PLATFORM.zip" "$PLATFORM_OUTPUT_DIR"

    echo "$PLATFORM build and zip completed."
	echo
done

echo "Build completed."

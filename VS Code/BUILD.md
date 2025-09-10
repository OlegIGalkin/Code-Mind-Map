# Build Instructions

This extension is now bundled using Webpack for better performance and smaller package size.

## Available Scripts

- `npm run bundle` - Create production bundle (used for packaging)
- `npm run bundle-dev` - Create development bundle with source maps for debugging
- `npm run watch` - Watch mode for development (rebuilds on file changes)
- `npm run package` - Bundle and package the extension
- `npm run compile` - Traditional TypeScript compilation (legacy)

## Debugging

The extension maintains full debugging capabilities:

1. **Source Maps**: Both production and development builds include source maps
2. **Development Build**: Use `npm run bundle-dev` for development with faster builds
3. **Watch Mode**: Use `npm run watch` for automatic rebuilding during development

## File Structure

- `webpack.config.js` - Production webpack configuration
- `webpack.dev.config.js` - Development webpack configuration
- `out/extension.js` - Bundled extension (single file)
- `out/extension.js.map` - Source map for debugging

## Benefits

- **Reduced Package Size**: From 4425 files to 11 files
- **Better Performance**: Single bundled file loads faster
- **Maintained Debugging**: Source maps preserve debugging capabilities
- **VS Code Compliance**: Follows VS Code extension bundling best practices

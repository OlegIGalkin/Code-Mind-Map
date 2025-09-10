const path = require('path');
const TerserPlugin = require('terser-webpack-plugin');

module.exports = {
  target: 'node',
  mode: 'production',
  entry: './src/extension.ts',
  output: {
    path: path.resolve(__dirname, 'out'),
    filename: 'extension.js',
    libraryTarget: 'commonjs2',
    clean: true
  },
  externals: {
    vscode: 'commonjs vscode',
    // Exclude all Node.js built-in modules to prevent bundling
    'path': 'commonjs path',
    'fs': 'commonjs fs',
    'util': 'commonjs util',
    'os': 'commonjs os',
    'crypto': 'commonjs crypto',
    'stream': 'commonjs stream',
    'events': 'commonjs events',
    'url': 'commonjs url',
    'querystring': 'commonjs querystring',
    'http': 'commonjs http',
    'https': 'commonjs https',
    'net': 'commonjs net',
    'tls': 'commonjs tls',
    'child_process': 'commonjs child_process',
    'cluster': 'commonjs cluster',
    'dgram': 'commonjs dgram',
    'dns': 'commonjs dns',
    'readline': 'commonjs readline',
    'repl': 'commonjs repl',
    'tty': 'commonjs tty',
    'vm': 'commonjs vm',
    'zlib': 'commonjs zlib'
  },
  resolve: {
    extensions: ['.ts', '.js'],
    // Prevent webpack from resolving these modules
    fallback: {
      "path": false,
      "fs": false,
      "util": false,
      "os": false,
      "crypto": false,
      "stream": false,
      "events": false,
      "url": false,
      "querystring": false,
      "http": false,
      "https": false,
      "net": false,
      "tls": false,
      "child_process": false,
      "cluster": false,
      "dgram": false,
      "dns": false,
      "readline": false,
      "repl": false,
      "tty": false,
      "vm": false,
      "zlib": false
    }
  },
  module: {
    rules: [
      {
        test: /\.ts$/,
        exclude: /node_modules/,
        use: [
          {
            loader: 'ts-loader',
            options: {
              // Enable tree shaking
              transpileOnly: false,
              compilerOptions: {
                // Enable dead code elimination
                removeComments: true,
                // Enable better tree shaking
                moduleResolution: 'node',
                allowSyntheticDefaultImports: true,
                esModuleInterop: true
              }
            }
          }
        ]
      }
    ]
  },
  optimization: {
    minimize: true,
    // Enable tree shaking
    usedExports: true,
    sideEffects: false,
    minimizer: [
      new TerserPlugin({
        terserOptions: {
          keep_classnames: true,
          keep_fnames: true,
          // More aggressive dead code elimination
          compress: {
            drop_console: true,
            drop_debugger: true,
            pure_funcs: ['console.log', 'console.info', 'console.debug', 'console.warn'],
            // Remove unused code
            unused: true,
            dead_code: true,
            // Remove unreachable code
            conditionals: true,
            evaluate: true,
            // Optimize boolean expressions
            booleans: true,
            // Remove unnecessary code
            if_return: true,
            join_vars: true,
            // Remove unused variables
            keep_fargs: false
          },
          mangle: {
            // Only mangle if it doesn't break VS Code extension API
            keep_classnames: true,
            keep_fnames: true
          }
        },
        // Enable parallel processing for faster builds
        parallel: true,
        // Extract comments to separate file (optional)
        extractComments: false
      })
    ]
  },
  // Disable source maps for production
  devtool: false,
  // Performance hints
  performance: {
    hints: 'warning',
    maxEntrypointSize: 512000,
    maxAssetSize: 512000
  }
};

const path = require('path');
const CopyWebpackPlugin = require('copy-webpack-plugin');

module.exports = {
    mode: 'production',
    entry: './src/extension.ts',
    target: 'node',
    output: {
        path: path.resolve(__dirname, 'out'),
        filename: 'extension.js',
        libraryTarget: 'commonjs2',
        clean: true
    },
    externals: {
        vscode: 'commonjs vscode' // the vscode-module is created on-the-fly and must be excluded
    },
    resolve: {
        extensions: ['.ts', '.js']
    },
    module: {
        rules: [
            {
                test: /\.ts$/,
                exclude: /node_modules/,
                use: [
                    {
                        loader: 'ts-loader'
                    }
                ]
            }
        ]
    },
    devtool: 'source-map',
    optimization: {
        minimize: false // Keep readable for debugging
    },
    plugins: [
        new CopyWebpackPlugin({
            patterns: [
                {
                    from: 'MindElixir/MindElixir.js',
                    to: 'MindElixir/MindElixir.js'
                }
            ]
        })
    ]
};

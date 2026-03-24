const path = require('path');
const HtmlWebpackPlugin = require('html-webpack-plugin');
const MiniCssExtractPlugin = require('mini-css-extract-plugin');
const ForkTsCheckerWebpackPlugin = require('fork-ts-checker-webpack-plugin');
const FileManagerPlugin = require('filemanager-webpack-plugin');

module.exports = (env = {}) => {
  const isProduction = env.production === true || env.production === 'true';
  const isDevServer = env.devServer === true || env.devServer === 'true';

  const rootDir = path.resolve(__dirname, '..', '..');
  const frontendDir = path.resolve(rootDir, 'frontend');
  const srcDir = path.resolve(frontendDir, 'src');
  const outputDir = path.resolve(rootDir, '_output', 'UI');

  return {
    mode: isProduction ? 'production' : 'development',
    target: 'web',

    entry: {
      app: path.resolve(srcDir, 'index.ts')
    },

    output: {
      path: outputDir,
      filename: isProduction ? 'static/js/[name].[contenthash:8].js' : 'static/js/[name].js',
      chunkFilename: isProduction
        ? 'static/js/[name].[contenthash:8].chunk.js'
        : 'static/js/[name].chunk.js',
      assetModuleFilename: 'static/media/[name][ext][query]',
      publicPath: '/',
      clean: true
    },

    devtool: isProduction ? 'source-map' : 'eval-cheap-module-source-map',

    resolve: {
      modules: [
        srcDir,
        'node_modules'
      ],
      fallback: {
        http: false,
        https: false,
        url: false,
        util: false
      },
      extensions: ['.ts', '.tsx', '.js', '.jsx', '.json']
    },

    module: {
      rules: [
        {
          test: /\.worker\.js$/,
          include: srcDir,
          use: [
            {
              loader: 'worker-loader'
            },
            {
              loader: 'babel-loader',
              options: {
                configFile: path.resolve(frontendDir, 'babel.config.js'),
                cacheDirectory: true
              }
            }
          ]
        },

        {
          test: /\.[jt]sx?$/,
          include: srcDir,
          exclude: [/node_modules/, /\.worker\.js$/],
          use: {
            loader: 'babel-loader',
            options: {
              configFile: path.resolve(frontendDir, 'babel.config.js'),
              cacheDirectory: true
            }
          }
        },

        {
          test: /\.css$/,
          include: srcDir,
          oneOf: [
            {
              test: [
                path.resolve(srcDir, 'index.css'),
                path.resolve(srcDir, 'Styles', 'globals.css'),
                path.resolve(srcDir, 'Styles', 'scaffolding.css'),
                path.resolve(srcDir, 'Content', 'Fonts', 'fonts.css')
              ],
              use: [
                isProduction ? MiniCssExtractPlugin.loader : 'style-loader',
                {
                  loader: 'css-loader',
                  options: {
                    importLoaders: 1,
                    sourceMap: !isProduction
                  }
                },
                {
                  loader: 'postcss-loader',
                  options: {
                    postcssOptions: {
                      config: path.resolve(frontendDir, 'postcss.config.js')
                    },
                    sourceMap: !isProduction
                  }
                }
              ]
            },
            {
              use: [
                isProduction ? MiniCssExtractPlugin.loader : 'style-loader',
                {
                  loader: 'css-loader',
                  options: {
                    importLoaders: 1,
                    sourceMap: !isProduction,
                    modules: {
                      exportLocalsConvention: 'asIs',
                      localIdentName: isProduction
                        ? '[hash:base64:8]'
                        : '[name]__[local]__[hash:base64:5]'
                    }
                  }
                },
                {
                  loader: 'postcss-loader',
                  options: {
                    postcssOptions: {
                      config: path.resolve(frontendDir, 'postcss.config.js')
                    },
                    sourceMap: !isProduction
                  }
                }
              ]
            }
          ]
        },

        {
          test: /\.(png|jpe?g|gif|svg|ico)$/i,
          type: 'asset/resource'
        },

        {
          test: /\.(woff2?|ttf|eot)$/i,
          type: 'asset/resource'
        }
      ]
    },

    plugins: [
      new HtmlWebpackPlugin({
        template: path.resolve(srcDir, 'index.ejs'),
        filename: 'index.html',
        inject: false,
        minify: isProduction
          ? {
              removeComments: true,
              collapseWhitespace: true,
              keepClosingSlash: true
            }
          : false
      }),

      new MiniCssExtractPlugin({
        filename: isProduction ? 'static/css/[name].[contenthash:8].css' : 'static/css/[name].css',
        chunkFilename: isProduction
          ? 'static/css/[name].[contenthash:8].chunk.css'
          : 'static/css/[name].chunk.css'
      }),

      ...(isDevServer
        ? []
        : [
            new ForkTsCheckerWebpackPlugin({
              typescript: {
                configFile: path.resolve(frontendDir, 'tsconfig.json')
              }
            })
          ]),

      new FileManagerPlugin({
        events: {
          onEnd: {
            copy: [
              {
                source: path.resolve(srcDir, 'Content'),
                destination: path.resolve(outputDir, 'Content')
              },
              {
                source: path.resolve(srcDir, 'login.html'),
                destination: path.resolve(outputDir, 'login.html')
              },
              {
                source: path.resolve(srcDir, 'Content', 'Images', 'Icons', 'favicon.ico'),
                destination: path.resolve(outputDir, 'favicon.ico')
              }
            ]
          }
        }
      })
    ],

    optimization: {
      splitChunks: {
        chunks: 'all'
      },
      runtimeChunk: 'single'
    },

    performance: {
      hints: false
    },

    devServer: isDevServer
      ? {
          host: '0.0.0.0',
          port: 3000,
          hot: false,
          liveReload: true,
          historyApiFallback: true,
          static: {
            directory: outputDir,
            publicPath: '/'
          },
          devMiddleware: {
            writeToDisk: false
          },
          proxy: [
            {
              context: [
                '/api',
                '/signalr',
                '/initialize.json',
                '/__URL_BASE__'
              ],
              target: 'http://localhost:5075',
              changeOrigin: true,
              ws: true
            }
          ]
        }
      : undefined,

    stats: 'minimal'
  };
};
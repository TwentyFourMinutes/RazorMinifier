# RazorMinifier

<img alt="GitHub issues" src="https://img.shields.io/github/issues-raw/TwentyFourMinutes/RazorMinifier"> <img alt="GitHub release (latest by date including pre-releases)" src="https://img.shields.io/github/v/release/TwentyFourMinutes/RazorMinifier?include_prereleases">

RazorMinifier is a free extension for Visual Studio 2019.
You can download RazorMinfier either from the Extension Manager in VisualStudio or from the official [Extension Market Place]( https://marketplace.visualstudio.com/items?itemName=twenty-four-minutes.razor-minifier ).

## About

With RazorMinifier you can minify `cshtml` and `html` files while still editing them with all intends and so on.
So what are the key-benefits about this extension? That is not as easy as you might think. 
In general minifying is already a pretty good approach for reducing file sizes by around 40%. Anyway compressing files with either `Brotli (br)` or `gzip` is an even better improvement, but this is not always possible.

If you have any website with which is secured by [TLS]( https://en.wikipedia.org/wiki/Transport_Layer_Security ) you are potentially opening security risks under specific circumstances. With that I am referring to [BREACH]( https://en.wikipedia.org/wiki/BREACH ) and [CRIME]( https://en.wikipedia.org/wiki/CRIME). For more details click on either of the attacks name or see [breachattack.com]( http://www.breachattack.com/ ). Although there are a few requirements that are needed in order to be exposed by the named attacks, see any of the links mentioned above.

With that said there is a pretty good chance that you don't want to compress your files anymore. Therefor minifying your html is a pretty good idea, anyhow editing inline html is a real pain in the ass and that is where RazorMinifier comes in to help you out.

## How to

The configuration file must be located on the root Directory of the Startup-Project and must be named `Rminify.json`. Although when you hit `Add/Remove Razor Minify` and the config file does not exist, it will automatically get generated for you. 

1. Create a `.cshtml` or `.html` file if you haven't already then hit right-click on this file. Which should you a few options as shown in the image. Now click on `Add/Remove Razor Minify`.

   ![Dialog](https://user-images.githubusercontent.com/36015290/67151966-8c260f00-f2ce-11e9-9629-e7dd59722c03.PNG)

2. Open the `.edit.cshtml` file in your project and as soon as you hit save, it will update the corresponding `.cshtml` file automatically.
3. If you want to remove a file from the minifying process either hit `Add/Remove Razor Minify` again or edit the `Rminfy.json` file, also you can add files the same way by editing the config file.

## Features

- Minifying of `cshtml` and `html` files on save
- Minifying of `js` files on save
- Inlining of `css` inside `cshtml` and `html` files on save
- Toggling addition/removal with a simple click
- Manual editing of the `Rminify.json` config file
- Maintains functionality of `cshtml` files after minifying
- Does only minimize deployment size

## Planned

- Support for `Razor` files regarding to [Blazor](https://blazor.net). 

## Notes

If you feel like something is not working as intended or you are experiencing issues, feel free to create an issue. Also for feature requests just create an issue. For further information feel free to send me a mail to `twenty@translucent.at` or message me on Discord `24_minutes#7496`.
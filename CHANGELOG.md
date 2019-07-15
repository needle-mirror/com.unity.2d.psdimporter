# Changelog
All notable changes to this package will be documented in this file.

The format is based on [Keep a Changelog](http://keepachangelog.com/en/1.0.0/)
and this project adheres to [Semantic Versioning](http://semver.org/spec/v2.0.0.html).

## [1.2.0-preview.3] - 2019-07-16
### Change
- Update to latest Animation dependency

## [1.2.0-preview.2] - 2019-06-07
### Added
- Change API to internal access
- Only generate Sprite Library Asset if there is entry
- Do not reset Reslice checkbox after Inspector apply

## [1.2.0-preview.1] - 2019-03-15
### Added
- Update support for 2019.2
- Integrate with 2D Animation Sprite Library
- Integrate with new 2D Animation Character Group
- Fix asset name conflict

## [1.1.0-preview.2] - 2019-04-23
### Added
- Fix potential name clashing issues with ScriptedImporter
- Fix Prefab asset using wrong name. Note this will break Prefab references if upgrading from previous versions.

## [1.1.0-preview.1] - 2019-02-19
### Added
- Update dependency for 2019.1 support

## [1.0.0-preview.3] - 2019-02-19
### Added
- Fix compilation error in .NET 3.5

## [1.0.0-preview.2] - 2019-01-25
### Added
- Fix unable to rig Sprites created manually
- Remove legacy packing tag
- Default Texture Type is changed to 'Sprite (2D and UI)'
- Default Sprite Mode is changed to 'Multiple'

## [1.0.0-preview.1] - 2018-11-20
### Added
- New release
- ScriptedImporter for importing Adobe Photoshop file
- Supports handling of Adobe Photoshop layers
    - Creates Sprites from individual layers
    - Handles include or exclude hidden layers
- Supports Prefab generation that reconstruct generated Sprites to original art asset layout
    - Prefab generation supports GameObject grouping based on Adobe Photoshop layer grouping
- Supports 2D Animation v2 single character with multiple Sprites workflow
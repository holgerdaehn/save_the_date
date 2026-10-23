// swift-tools-version:6.0
import PackageDescription

let package = Package(
    name: "SaveTheDate",
    platforms: [.macOS(.v15)],
    targets: [
        .executableTarget(name: "SaveTheDate", path: "Sources"),
        .testTarget(name: "SaveTheDateTests", dependencies: ["SaveTheDate"], path: "Tests"),
    ],
    swiftLanguageModes: [.v5]
)

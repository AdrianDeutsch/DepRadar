namespace DepRadar.Application.Ecosystems;

/// <summary>
/// Curated lists of each ecosystem's most-downloaded packages — the names typosquatters
/// actually imitate. Embedded (rather than fetched) so the lookalike check is
/// deterministic, offline and testable; the set only needs to cover the popular
/// targets, not the whole registry.
/// </summary>
public static class KnownPackages
{
    /// <summary>Top npm packages (canonical lower-case).</summary>
    public static readonly IReadOnlyList<string> Npm =
    [
        "lodash", "react", "react-dom", "express", "axios", "chalk", "commander", "debug",
        "moment", "prop-types", "request", "tslib", "webpack", "vue", "jquery", "typescript",
        "uuid", "classnames", "yargs", "glob", "inquirer", "rxjs", "core-js", "dotenv",
        "body-parser", "colors", "minimist", "semver", "async", "bluebird", "underscore",
        "redux", "next", "styled-components", "eslint", "prettier", "jest", "mocha",
        "socket.io", "mongoose", "passport", "cheerio", "nodemon", "cors", "morgan",
        "js-yaml", "rimraf", "mkdirp", "fs-extra", "node-fetch", "isomorphic-fetch",
        "cross-env", "babel-core", "electron", "puppeteer", "sharp", "winston", "pino",
        "date-fns", "dayjs", "ramda", "immer", "zod", "graphql", "apollo-server", "vite",
        "esbuild", "rollup", "tailwindcss", "postcss", "autoprefixer", "sass", "less",
        "html-webpack-plugin", "ts-node", "nan", "form-data", "qs", "cookie", "ws",
        "mime", "open", "ora", "boxen", "figlet", "shelljs", "execa", "got", "superagent",
    ];

    /// <summary>Top PyPI packages (PEP 503 canonical form).</summary>
    public static readonly IReadOnlyList<string> PyPi =
    [
        "requests", "urllib3", "boto3", "botocore", "certifi", "idna", "charset-normalizer",
        "setuptools", "python-dateutil", "numpy", "pandas", "six", "pyyaml", "cryptography",
        "packaging", "pip", "wheel", "click", "flask", "django", "jinja2", "markupsafe",
        "werkzeug", "itsdangerous", "sqlalchemy", "pytest", "attrs", "pluggy", "colorama",
        "pillow", "scipy", "matplotlib", "scikit-learn", "tensorflow", "torch", "keras",
        "jsonschema", "pyparsing", "aiohttp", "httpx", "fastapi", "uvicorn", "pydantic",
        "starlette", "gunicorn", "celery", "redis", "psycopg2", "pymongo", "lxml",
        "beautifulsoup4", "selenium", "openpyxl", "xlrd", "pytz", "tzdata", "rich",
        "typer", "tqdm", "regex", "toml", "tomli", "virtualenv", "tox", "black", "flake8",
        "mypy", "isort", "coverage", "paramiko", "fabric", "ansible", "docker", "kubernetes",
        "google-api-python-client", "protobuf", "grpcio", "websockets", "python-dotenv",
        "environs", "marshmallow", "alembic", "greenlet",
    ];

    /// <summary>Top crates.io packages (lower-case).</summary>
    public static readonly IReadOnlyList<string> Cargo =
    [
        "serde", "serde-json", "serde_json", "syn", "quote", "proc-macro2", "rand",
        "libc", "cfg-if", "tokio", "futures", "log", "env-logger", "env_logger", "clap",
        "regex", "lazy-static", "lazy_static", "itertools", "thiserror", "anyhow",
        "chrono", "bytes", "hyper", "reqwest", "url", "uuid", "base64", "bitflags",
        "hashbrown", "indexmap", "smallvec", "parking-lot", "parking_lot", "crossbeam",
        "rayon", "num-traits", "memchr", "once-cell", "once_cell", "tracing", "axum",
        "actix-web", "rocket", "diesel", "sqlx", "rustls", "openssl", "ring", "sha2",
        "hmac", "aes", "zeroize", "wasm-bindgen", "js-sys", "web-sys", "criterion",
        "tempfile", "walkdir", "glob", "dirs", "toml", "yaml-rust", "csv", "flate2",
        "zip", "tar", "image", "nalgebra", "ndarray", "petgraph", "semver", "time",
    ];

    /// <summary>Top NuGet packages (lower-case).</summary>
    public static readonly IReadOnlyList<string> NuGet =
    [
        "newtonsoft.json", "serilog", "nlog", "log4net", "automapper", "mediatr",
        "fluentvalidation", "fluentassertions", "moq", "nsubstitute", "xunit", "nunit",
        "dapper", "polly", "swashbuckle.aspnetcore", "restsharp", "castle.core",
        "system.text.json", "microsoft.extensions.logging", "microsoft.extensions.configuration",
        "microsoft.extensions.dependencyinjection", "microsoft.entityframeworkcore",
        "npgsql", "mysql.data", "stackexchange.redis", "mongodb.driver", "elasticsearch.net",
        "rabbitmq.client", "confluent.kafka", "masstransit", "hangfire", "quartz",
        "grpc.net.client", "google.protobuf", "azure.storage.blobs", "awssdk.core",
        "aws-sdk-net", "sharpziplib", "csvhelper", "closedxml", "epplus", "itext7",
        "sixlabors.imagesharp", "skiasharp", "identitymodel", "jwt", "bcrypt.net-next",
        "humanizer", "bogus", "benchmarkdotnet", "coverlet.collector", "shouldly",
    ];
}

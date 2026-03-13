# Conventional Commits

I’d like to introduce conventional commits to help provide some detail on creating solid commit messages.

---

Here’s a great template for a good commit message:

```text

<type>[optional scope]: <description>

[optional body]

[optional footer(s)]

```

- feat: Commits, which adds a new feature
- fix: Commits, that fixes a bug
- refactor: refactored code that neither fixes a bug nor adds a feature but rewrites/restructures your code.
- chore : Changes that do not relate to a fix or feature and don’t modify src or test files basically miscellaneous commits (for example, updating dependencies or modifying .gitignore file)
- perf : Commits are special refactor commits, geared towards improving performance.
- ci : Continuous integration related.
- ops : Commits, that affect operational components like infrastructure, deployment, backup , recovery …
- build : Changes that affect the build system build tool, ci pipeline, dependencies, project version, …
- docs : Commits, that affect documentation, such as the README.
- style : changes that do not affect the meaning of the code, likely related to code formatting such as white-space, missing semi-colons, etc.
- revert: reverts a previous commit.
- test:commits that add missing tests or correct existing tests

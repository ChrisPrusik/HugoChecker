# HugoChecker action

This GitHub Ation will check all markdown files in the [Hugo](https://gohugo.io) project for their correctness based on the configuration file.

* Spellcheck.
* Checking whether the correct language versions are contained in the headers and text based on the file name.
* Checking if all language versions of markdown files are in the folder based on project configuration.
* And [more...]()

Example usage:

```yaml
runs:
  steps:
    - name: Check out the Hugo *.md files
      uses: HugoChecker
      with:
        hugo-folder: ${{ github.workspace }}/test-hugo
```
The input `hugo-folder` is the root folder of the Hugo project.

> **NOTE:** 
>
> * The current version of the action operates only on yaml configurations.
> * Language code must be in two letter: en, pl etc.

## Configuration files

The root folder should also contain this action configuration file `hugo-checker.yaml` and Hugo configuration file `config.yaml`.

## HugoChecker configuration file

File `hugo-checker.yaml` - example configuration:

```yaml
folders-to-check:
  - content
  - content/blog
required-headers:
  - slug
  - title
  - description
  - author
  - date
required:
  - section:
    - en:
      - Personal experience
      - Existential question
      - Practice
    - pl:
      - Osobiste doświadczenie
      - Egzystencjalne pytanie
      - Praktyka
  - category:
    - en:
      - Blog
      - App
      - Information
    - pl:
      - Blog
      - Apka
      - Informacja
check-file-names: yes
check-file-language: yes
translated-newer-than-original: yes
spellcheck: yes
original-language: pl
translated-language: en
```

### Switch folders-to-check

List of folders containing markdown `*.md` files to check. 

Example usage:

```yaml
folders-to-check:
  - content
  - content/blog
```

## Switch required-headers

List of headers that must be defined in each markdown file.

Example usage:

```yaml
required-headers:
  - slug
  - title
  - description
  - author
  - date
```











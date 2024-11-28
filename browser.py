import argparse
from urllib.parse import urlparse, unquote
import requests
import logging
from bs4 import BeautifulSoup
from colorama import init, Fore, Style

def search_duckduckgo(query):
    query = query.replace(" ", "+")
    headers = {
        'User-Agent': 'Mozilla/4.0 (compatible; MSIE 8.0; Windows NT 5.1; Trident/4.0)'
    }
    url = f"https://duckduckgo.com/html/?q={query}"
    logging.info(f"Searching DuckDuckGo for: {query}")
    response = requests.get(url, headers=headers)
    logging.info(f"Received response with status code: {response.status_code}")
    return response.text

def parse_results(html, valid_websites):
    logging.info("Parsing HTML to extract results")
    soup = BeautifulSoup(html, 'html.parser')
    results = []
    for result in soup.find_all('a', class_='result__a'):
        link = result['href']
        title = result.text
        if any(website in link for website in valid_websites):
            results.append((title, link))
    logging.info(f"Found {len(results)} valid results")
    return results

def display_results(results):
    init()
    logging.info("Displaying results")
    for title, l in results:
        link = remove_parameters_and_decode(l.replace("//duckduckgo.com/l/?uddg=", ""))
        print(f"{Fore.GREEN}{title}{Style.RESET_ALL}: {Fore.BLUE}{link}{Style.RESET_ALL}")


def remove_parameters_and_decode(url):
    # Parse the URL
    parsed_url = urlparse(url)

    # Construct the URL without the query parameters
    url_without_params = f"{parsed_url.scheme}://{parsed_url.netloc}{parsed_url.path}"

    # Decode the path
    decoded_path = unquote(parsed_url.path)

    return decoded_path

def main():
    valid_websites = [
        'reddit.com',
        'stackoverflow.com',
        'stackexchange.com',
        'medium.com'
    ]

    parser = argparse.ArgumentParser(description='Search DuckDuckGo and filter results from valid websites.')
    parser.add_argument('search_term', type=str, help='The search term to look up on DuckDuckGo.')
    args = parser.parse_args()

    search_term = args.search_term
    logging.info(f"Starting search for term: {search_term}")
    html = search_duckduckgo(search_term)
    results = parse_results(html, valid_websites)
    
    if results:
        display_results(results)
    else:
        logging.info("No results found from the valid websites")
        print("No results found from the valid websites.")

if __name__ == "__main__":
    logging.basicConfig(level=logging.DEBUG, format='%(asctime)s - %(levelname)s - %(message)s')
    main()
